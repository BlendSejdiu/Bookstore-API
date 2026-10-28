# Bookstore API

An ASP.NET Core API for searching and managing books. It uses SQL Server for storage and Keycloak for authentication and authorization.

## Prerequisites

- .NET 10 SDK
- Docker Desktop with Docker Compose, for SQL Server and Keycloak
- A browser and PowerShell

The commands below run from the repository root. They start SQL Server and Keycloak in Docker and run the API locally. The included Compose configuration can also build the API container; see the container section below.

## 1. Start SQL Server and Keycloak

Create a `.env` file in the repository root (or update your existing one) with values of your choice:

```dotenv
SQL_SA_PASSWORD=ReplaceWithAStrongPassword1!
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=ReplaceWithAnotherStrongPassword1!
```

Use a password that meets SQL Server's password policy. Do not commit this file or share its values. Then run:

```powershell
docker compose up -d sqlserver keycloak
docker compose logs -f sqlserver keycloak
```

Wait until both services are ready, then press Ctrl+C to leave the log view. Keycloak is at `http://localhost:8081` and SQL Server is at `localhost,1433`. The SQL Server volume and Keycloak data directory persist between runs; changing credentials in `.env` does not necessarily change credentials in an existing database.

## 2. Configure and run the API

The checked-in SQL Server connection string points to a specific machine. Override it for the current PowerShell session using the same SQL Server password as in `.env`:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost,1433;Database=BookstoreDb;User Id=sa;Password=ReplaceWithAStrongPassword1!;TrustServerCertificate=True'
dotnet dev-certs https --trust
dotnet restore Bookstore-API.slnx
dotnet run --project Bookstore-API --launch-profile https
```

Use your actual password in the connection string. In development, startup applies database migrations and seeds authors if the authors table is empty. The HTTPS launch profile serves Swagger at [https://localhost:7027/swagger/index.html](https://localhost:7027/swagger/index.html). If startup fails while SQL Server is still starting, wait for readiness and retry.

### Local Keycloak metadata mismatch

The checked-in development configuration uses the HTTP issuer and discovery address `http://localhost:8081/realms/master`, but `Extensions/DependencyInjection.cs` currently sets `RequireHttpsMetadata` to `true` in development. JWT authentication therefore cannot retrieve that HTTP metadata as configured. For local development, change that assignment to:

```csharp
opt.RequireHttpsMetadata = !services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>().IsDevelopment();
```

Restart the API after changing it. This permits HTTP metadata only in development; use HTTPS for Keycloak and discovery outside local development. This README does not apply the code change automatically.

## 3. Configure Keycloak

Open [http://localhost:8081](http://localhost:8081), sign in to the administration console using your `.env` credentials, and select the `master` realm. The project does not import a realm configuration, so create or verify the following settings. Console labels may vary by Keycloak version.

1. Create an OpenID Connect client named `bookstore-api` to represent the API. Under its **Roles**, create `books-search` and `books-manage`.
2. Create an OpenID Connect public client named `bookstore-web`. Enable the **Implicit flow**, and add `https://localhost:7027/swagger/oauth2-redirect.html` as a valid redirect URI. Add `https://localhost:7027` to valid web origins if required. Restrict other flows to those you intend to use.
3. Create a user with a password, and assign that user the **client role** `bookstore-api` → `books-search`.
4. Create an OpenID Connect client named `bookstore-service`. Enable client authentication and service-account roles. Assign its service account the **client role** `bookstore-api` → `books-manage`. Copy its client secret from the client credentials page. Keep it private.
5. Create a client scope, for example `bookstore-api-access`, with an **Audience** mapper that includes client audience `bookstore-api` in the access token. Attach the scope as **Default** to both `bookstore-web` and `bookstore-service`.
6. Ensure the effective role scopes allow the respective `bookstore-api` roles in the tokens. If **Full scope allowed** is disabled, explicitly allow the relevant client roles. Verify newly issued tokens contain an `aud` including `bookstore-api`, `azp` matching the client, and the assigned role in `resource_access.bookstore-api.roles`.

The API validates the issuer, audience, token signature, expiry and the `typ` claim as well as its authorization policies. The default issuer is `http://localhost:8081/realms/master`; if you use another realm or address, update `Keycloak:Issuer`, `Keycloak:MetadataAddress`, and, if needed, `Keycloak:Audience` consistently. The issuer must match the token's `iss` exactly.

## 4. Authorize in Swagger

The API uses different clients and roles for search and management:

| Operation | Required client (`azp`) | Required `bookstore-api` client role |
| --- | --- | --- |
| `GET /api/books` | `bookstore-web` | `books-search` |
| `GET /api/books/{id}`, `POST`, `PUT`, `DELETE` | `bookstore-service` | `books-manage` |

### Search as a user

1. Open Swagger and select **Authorize**.
2. Under `bookstore-web`, choose the `openid` scope if prompted, then authorize.
3. Sign in as the Keycloak user with `books-search` and return to Swagger.
4. Expand `GET /api/books`, select **Try it out**, and execute it. Optional query parameters are `title`, `author`, `page` (default 1), and `pageSize` (default 10, maximum 100).

### Manage books as a service

Obtain a service access token from Keycloak. In PowerShell, replace the placeholder with the `bookstore-service` client secret:

```powershell
$tokenResponse = Invoke-RestMethod -Method Post `
  -Uri 'http://localhost:8081/realms/master/protocol/openid-connect/token' `
  -ContentType 'application/x-www-form-urlencoded' `
  -Body @{ grant_type = 'client_credentials'; client_id = 'bookstore-service'; client_secret = 'YOUR_CLIENT_SECRET' }
$token = $tokenResponse.access_token
```

In Swagger, select **Authorize**, paste the access token into the `service-token` bearer field, and authorize. Use the raw token without the `Bearer ` prefix; Swagger adds it. You can then call the management endpoints. Do not paste the secret into Swagger or commit it to the repository.

For example, a create or update request body is:

```json
{
  "title": "Example book",
  "subTitle": "An example",
  "author": { "authorId": 1, "name": "George Orwell" }
}
```

Use an existing author ID. On a fresh database the seed creates five authors, but IDs in an existing database may differ. The API uses the supplied author ID to look up the author.

A **401** usually means the token is missing, expired, invalid, or cannot be validated. A **403** means authentication succeeded but the client or role does not satisfy the endpoint's policy. Check the claims on a newly issued token and inspect API logs; do not share access tokens or client secrets.

## Running the API in Docker

With `.env` configured, `docker compose up -d --build` starts all three services. The API maps host HTTP port 8082 and HTTPS port 8083; Swagger is available only in the Development environment. The container's discovery address is configured as `http://host.docker.internal:8081/realms/master/.well-known/openid-configuration`, while its issuer remains `http://localhost:8081/realms/master`. The local metadata mismatch above still applies.

The Compose override mounts local HTTPS certificate directories but does not itself configure a certificate for Kestrel. Configure a trusted certificate and its Kestrel settings before using `https://localhost:8083/swagger/index.html`, and register `https://localhost:8083/swagger/oauth2-redirect.html` as an additional redirect URI in `bookstore-web`. Ensure discovery and the advertised signing-keys URL are reachable **from the API container**; an advertised `localhost` address can refer to the container itself. SQL Server may need extra time to become ready, so inspect `docker compose logs bookstore-api sqlserver` and restart the API if needed.

Stop the services with `docker compose down`. This leaves the persisted database and Keycloak data in place.
