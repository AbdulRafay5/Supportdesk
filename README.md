# SupportDesk

An IT helpdesk application for reporting, assigning and resolving support tickets.

## Planned stack
ASP.NET Core, C#, React, TypeScript, SQL Server, Entity Framework Core and xUnit.

## Run the initial API
Requires .NET 10 SDK.

```sh
dotnet run --project src/SupportDesk.Api -- --urls http://localhost:5080
```

Endpoints: GET /health, GET /api/tickets, GET /api/tickets/{id}, POST /api/tickets (JSON title and description).

Tickets are currently stored in memory and lost on restart. Authentication, persistent storage and the frontend are planned, not implemented. Run locally only.
