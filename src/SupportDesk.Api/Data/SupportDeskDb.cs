using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SupportDesk.Api.Models;

namespace SupportDesk.Api.Data;

public sealed class SupportDeskDb(DbContextOptions<SupportDeskDb> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var ticket = builder.Entity<Ticket>();
        ticket.HasOne<AppUser>().WithMany().HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasKey(t => t.Id);
        ticket.Property(t => t.Title).HasMaxLength(120).IsRequired();
        ticket.Property(t => t.Description).HasMaxLength(4000).IsRequired();
        ticket.Property(t => t.Status).HasMaxLength(20).IsRequired();
    }
}
