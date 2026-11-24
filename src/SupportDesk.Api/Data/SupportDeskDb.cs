using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Models;

namespace SupportDesk.Api.Data;

public sealed class SupportDeskDb(DbContextOptions<SupportDeskDb> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var ticket = builder.Entity<Ticket>();
        ticket.HasKey(t => t.Id);
        ticket.Property(t => t.Title).HasMaxLength(120).IsRequired();
        ticket.Property(t => t.Description).HasMaxLength(4000).IsRequired();
        ticket.Property(t => t.Status).HasMaxLength(20).IsRequired();
    }
}
