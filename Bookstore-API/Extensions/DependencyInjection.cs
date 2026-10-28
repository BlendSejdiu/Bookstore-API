using Bookstore_API.Database.Persistence;
using Bookstore_API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Bookstore_API.Extensions;

public static class DependecyInjection
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));

        return services;
    }

    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services, string issuer)
    {

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["bookstore-web"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "Sign in as a user with the books-search role.",
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{issuer.TrimEnd('/')}/protocol/openid-connect/auth"),
                            Scopes = new Dictionary<string, string> { ["openid"] = "OpenID Connect" }
                        }
                    }
                };
                document.Components.SecuritySchemes["service-token"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token obtained separately using bookstore-service client credentials."
                };

                foreach (var (path, item) in document.Paths)
                {
                    if (!path.StartsWith("/api/books", StringComparison.OrdinalIgnoreCase) || item.Operations is null)
                        continue;
                    foreach (var (method, operation) in item.Operations)
                    {
                        var scheme = path.Equals("/api/books", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Get
                            ? "bookstore-web" : "service-token";
                        operation.Security = [new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(scheme, document)] = []
                }];
                    }
                }
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static IServiceCollection AddAuthenticationAndAuthorization(this IServiceCollection services, string metadataAddress, string issuer, string audience)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
        {
            opt.MetadataAddress = metadataAddress;
            opt.RequireHttpsMetadata = services.BuildServiceProvider().GetService<IWebHostEnvironment>()!.IsDevelopment();
            opt.MapInboundClaims = false;
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                IssuerValidator = (tokenIssuer, _, _) => string.Equals(tokenIssuer, issuer, StringComparison.Ordinal) ? tokenIssuer
                                    : throw new SecurityTokenInvalidIssuerException("The token issuer is invalid."),
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };
            opt.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    if (context.Principal?.FindFirst("typ")?.Value != "Bearer")
                        context.Fail("A Keycloak access token is required.");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("SearchBooks", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("azp", "bookstore-web");
                policy.RequireAssertion(context => UserClaims.HasBookstoreApiRole(context.User, "books-search"));
            })
            .AddPolicy("ManageBooks", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("azp", "bookstore-service");
                policy.RequireAssertion(context => UserClaims.HasBookstoreApiRole(context.User, "books-manage"));
            });

        return services;
    }
}
