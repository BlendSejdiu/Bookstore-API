using Bookstore_API.Database.Persistence;
using Bookstore_API.Services;
using Bookstore_API.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var keycloak = builder.Configuration.GetSection("Keycloak");
var issuer = keycloak["Issuer"] ?? throw new InvalidOperationException("Keycloak:Issuer is required.");
var metadataAddress = keycloak["MetadataAddress"] ?? throw new InvalidOperationException("Keycloak:MetadataAddress is required.");
var audience = keycloak["Audience"] ?? throw new InvalidOperationException("Keycloak:Audience is required.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
    {
        opt.MetadataAddress = metadataAddress;
        opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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

builder.Services.AddAuthorization(opt => 
    {
        opt.AddPolicy("SearchBooks", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("azp", "bookstore-web");
            policy.RequireAssertion(context => UserClaims.HasBookstoreApiRole(context.User, "books-search"));
        });

        opt.AddPolicy("ManageBooks", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("azp", "bookstore-service");
            policy.RequireAssertion(context => UserClaims.HasBookstoreApiRole(context.User, "books-manage"));
        });
    });


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddScoped<IBookService, BookService>();

builder.Services.AddOpenApi(options =>
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Bookstore API v1");
        options.OAuthClientId("bookstore-web");
        options.OAuthScopes("openid");
    });

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
