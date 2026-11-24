namespace SupportDesk.Api.Models;

public sealed record Ticket(Guid Id, string Title, string Description, string Status, DateTimeOffset CreatedAt);
public sealed record CreateTicket(string Title, string Description);
