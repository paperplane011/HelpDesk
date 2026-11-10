using HelpDesk.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Infrastructure;

public sealed class HelpDeskDbContext(DbContextOptions<HelpDeskDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> Comments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Ticket>(b =>
        {
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Description).HasMaxLength(10000).IsRequired();
            b.Property(t => t.Version).IsConcurrencyToken();
            b.Property(t => t.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            b.Property(t => t.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            b.Property(t => t.ClosedAt).HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(t => new { t.OwnerId, t.CreatedAt });
            b.HasIndex(t => new { t.Status, t.Priority, t.CreatedAt });
        });
        builder.Entity<TicketComment>(b =>
        {
            b.Property(c => c.Body).HasMaxLength(4000).IsRequired();
            b.Property(c => c.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            b.HasOne<Ticket>().WithMany().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(c => new { c.TicketId, c.CreatedAt });
        });
    }
}
