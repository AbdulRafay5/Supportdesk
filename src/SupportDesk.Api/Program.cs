using SupportDesk.Api.Models;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var tickets = new ConcurrentDictionary<Guid, Ticket>();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/tickets", () => tickets.Values.OrderByDescending(t => t.CreatedAt).ToArray());
app.MapGet("/api/tickets/{id:guid}", (Guid id) =>
    tickets.TryGetValue(id, out var ticket) ? Results.Ok(ticket) : Results.NotFound());
app.MapPost("/api/tickets", (CreateTicket request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 ||
        string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 4000)
        return Results.BadRequest(new { error = "Title (1-120) and description (1-4000) are required." });

    var ticket = new Ticket(Guid.NewGuid(), request.Title.Trim(), request.Description.Trim(),
        "Open", DateTimeOffset.UtcNow);
    tickets[ticket.Id] = ticket;
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
});
app.Run();
