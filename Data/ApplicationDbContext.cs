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
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealItem> MealItems => Set<MealItem>();

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

        builder.Entity<Meal>(e =>
        {
            e.HasIndex(m => new { m.UserId, m.CreatedAt });
            e.HasOne(m => m.User).WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(m => m.Title).HasMaxLength(150);
            e.Property(m => m.Confidence).HasMaxLength(10);
            e.Property(m => m.Notes).HasMaxLength(1000);
            e.Property(m => m.PhotoPath).HasMaxLength(100);
        });

        builder.Entity<MealItem>(e =>
        {
            e.HasOne(i => i.Meal).WithMany(m => m.Items)
                .HasForeignKey(i => i.MealId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.Name).HasMaxLength(100);
            e.Property(i => i.Grams).HasPrecision(7, 1);
            e.Property(i => i.Kcal).HasPrecision(7, 1);
            e.Property(i => i.ProteinG).HasPrecision(6, 1);
            e.Property(i => i.CarbsG).HasPrecision(6, 1);
            e.Property(i => i.FatG).HasPrecision(6, 1);
        });
    }
}