using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kalories.Server.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<BodyMeasurement> Measurements => Set<BodyMeasurement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .Property(u => u.ConsentVersion)
            .HasMaxLength(20);

        builder.Entity<UserProfile>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne(p => p.User).WithOne()
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.HeightCm).HasPrecision(5, 1);
        });

        builder.Entity<BodyMeasurement>(e =>
        {
            e.HasIndex(m => new { m.UserId, m.MeasuredAt });
            e.HasOne(m => m.User).WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(m => m.WeightKg).HasPrecision(5, 2);
            e.Property(m => m.WaistCm).HasPrecision(5, 1);
            e.Property(m => m.HipCm).HasPrecision(5, 1);
            e.Property(m => m.ChestCm).HasPrecision(5, 1);
        });
    }
}