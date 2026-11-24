namespace SupportDesk.Api.Models;

public sealed record Ticket(Guid Id, string Title, string Description, string Status, DateTimeOffset CreatedAt, string OwnerId);
public sealed record CreateTicket(string Title, string Description);
public sealed record UpdateTicket(string Title, string Description, string Status);
