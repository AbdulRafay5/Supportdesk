using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Data;
using SupportDesk.Api.Models;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("SupportDesk")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:SupportDesk before starting.");
builder.Services.AddDbContext<SupportDeskDb>(options => options.UseSqlServer(connection));
var app = builder.Build();

// Temporary local development bootstrap until migrations are introduced.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SupportDeskDb>().Database.EnsureCreatedAsync();
}
app.MapGet("/health", async (SupportDeskDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapGet("/api/tickets", async (SupportDeskDb db, CancellationToken ct) =>
    await db.Tickets.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync(ct));
app.MapGet("/api/tickets/{id:guid}", async (Guid id, SupportDeskDb db, CancellationToken ct) =>
    await db.Tickets.FindAsync(new object[] { id }, ct) is { } ticket ? Results.Ok(ticket) : Results.NotFound());
app.MapPost("/api/tickets", async (CreateTicket request, SupportDeskDb db, CancellationToken ct) =>
{
    if (!Valid(request.Title, request.Description)) return Results.BadRequest(new { error = "Title (1-120) and description (1-4000) are required." });
    var ticket = new Ticket(Guid.NewGuid(), request.Title.Trim(), request.Description.Trim(), "Open", DateTimeOffset.UtcNow);
    db.Tickets.Add(ticket);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
});
app.MapPut("/api/tickets/{id:guid}", async (Guid id, UpdateTicket request, SupportDeskDb db, CancellationToken ct) =>
{
    if (!Valid(request.Title, request.Description) || request.Status is not ("Open" or "InProgress" or "Resolved"))
        return Results.BadRequest(new { error = "Invalid title, description or status." });
    var ticket = await db.Tickets.FindAsync(new object[] { id }, ct);
    if (ticket is null) return Results.NotFound();
    db.Entry(ticket).CurrentValues.SetValues(ticket with { Title = request.Title.Trim(), Description = request.Description.Trim(), Status = request.Status });
    await db.SaveChangesAsync(ct);
    return Results.Ok(ticket);
});
app.MapDelete("/api/tickets/{id:guid}", async (Guid id, SupportDeskDb db, CancellationToken ct) =>
{
    var ticket = await db.Tickets.FindAsync(new object[] { id }, ct);
    if (ticket is null) return Results.NotFound();
    db.Tickets.Remove(ticket);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
app.Run();

static bool Valid(string? title, string? description) =>
    !string.IsNullOrWhiteSpace(title) && title.Length <= 120 &&
    !string.IsNullOrWhiteSpace(description) && description.Length <= 4000;
