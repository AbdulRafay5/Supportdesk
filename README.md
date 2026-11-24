# SupportDesk

An IT helpdesk application for reporting, assigning and resolving support tickets.

## Stack
ASP.NET Core, C#, SQL Server and Entity Framework Core. React, TypeScript and automated tests are planned.

## Local development
Requires the .NET 10 SDK and a running SQL Server instance. In PowerShell, configure a connection string for your local database (example uses Windows LocalDB):

```powershell
$env:ConnectionStrings__SupportDesk = 'Server=(localdb)\MSSQLLocalDB;Database=SupportDesk;Integrated Security=true'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/SupportDesk.Api -- --urls http://localhost:5080
```

Development mode creates the database schema if absent. Migrations are not yet implemented. Do not expose this unauthenticated API publicly.

## API
- GET /health
- GET /api/tickets
- GET /api/tickets/{id}
- POST /api/tickets (title and description)
- PUT /api/tickets/{id} (title, description and status: Open, InProgress or Resolved)
- DELETE /api/tickets/{id}

Database integration is implemented but has not yet been runtime verified.
