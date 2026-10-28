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
BOOKSTORE_SERVICE_CLIENT_SECRET=ReplaceWithARandomClientSecret
```

Use a password that meets SQL Server's password policy. Do not commit this file or share its values. Then run:

```powershell
docker compose up -d sqlserver keycloak
docker compose logs -f sqlserver keycloak
```

Wait until both services are ready, then press Ctrl+C to leave the log view. Keycloak is at `http://localhost:8081` and SQL Server is at `localhost,1433`. The SQL Server volume and Keycloak data directory persist between runs; changing credentials in `.env` does not change credentials in existing data. Keycloak imports the `bookstore` realm on first startup if that realm does not already exist. Existing realms are skipped; the import does not overwrite them.

## 2. Configure and run the API

The checked-in SQL Server connection string points to a specific machine. Override it for the current PowerShell session using the same SQL Server password as in `.env`:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost,1433;Database=BookstoreDb;User Id=sa;Password=ReplaceWithAStrongPassword1!;TrustServerCertificate=True'
dotnet dev-certs https --trust
dotnet restore Bookstore-API.slnx
dotnet run --project Bookstore-API --launch-profile https
```

Use your actual password in the connection string. In development, startup applies database migrations and seeds authors if the authors table is empty. The HTTPS launch profile serves Swagger at [https://localhost:7027/swagger/index.html](https://localhost:7027/swagger/index.html). If startup fails while SQL Server is still starting, wait for readiness and retry.






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


```powershell
$tokenResponse = Invoke-RestMethod -Method Post `
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



Stop the services with `docker compose down`. This leaves the persisted database and Keycloak data in place.
