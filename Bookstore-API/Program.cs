using Bookstore_API.Database.Persistence;
using Bookstore_API.Extensions;
using Bookstore_API.Services;
using Bookstore_API.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var keycloak = builder.Configuration.GetSection("Keycloak");
var issuer = keycloak["Issuer"] ?? throw new InvalidOperationException("Keycloak:Issuer is required.");
var metadataAddress = keycloak["MetadataAddress"] ?? throw new InvalidOperationException("Keycloak:MetadataAddress is required.");
var audience = keycloak["Audience"] ?? throw new InvalidOperationException("Keycloak:Audience is required.");

builder.Services.AddAuthenticationAndAuthorization(metadataAddress, issuer, audience, builder.Environment.IsDevelopment());

builder.Services.AddDatabase(builder.Configuration);

builder.Services.AddScoped<IBookService, BookService>();

builder.Services.AddSwaggerConfiguration(issuer);

var app = builder.Build();

app.UseExceptionHandler();

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
