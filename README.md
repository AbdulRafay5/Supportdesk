# SupportDesk

An IT helpdesk application for reporting, assigning and resolving support tickets.

## Stack
ASP.NET Core, C#, SQL Server and Entity Framework Core. React, TypeScript and automated tests are planned.

## Local development
Requires the .NET 10 SDK and a running SQL Server instance. In PowerShell, configure a connection string for your local database (example uses Windows LocalDB):

```powershell
$env:ConnectionStrings__SupportDesk = 'Server=(localdb)\MSSQLLocalDB;Database=SupportDeskAuth;Integrated Security=true'
$env:Jwt__Key = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/SupportDesk.Api -- --urls http://localhost:5080
```

Development mode creates the database schema if absent. Migrations are not yet implemented. Use a fresh development database for the new identity schema. Do not expose this API publicly until HTTPS, migrations and security tests are in place.

## API
- GET /health
- GET /api/tickets
- GET /api/tickets/{id}
- POST /api/tickets (title and description)
- PUT /api/tickets/{id} (title, description and status: Open, InProgress or Resolved)
- DELETE /api/tickets/{id}

Database integration is implemented but has not yet been runtime verified.

## Authentication
POST /api/auth/register and POST /api/auth/login accept email and password. Registration creates employees only. Send the login token as Authorization: Bearer TOKEN for ticket requests. Employees see their own tickets; agents and admins see all tickets; only admins delete tickets. Set Bootstrap__Email and Bootstrap__Password through local environment variables before the first development startup to create an admin. Admins can assign Employee or Agent via PUT /api/users/{id}/role. Existing tokens expire after 30 minutes; role changes are not immediately reflected in issued tokens. Never commit secrets. Authentication has not been runtime tested.
