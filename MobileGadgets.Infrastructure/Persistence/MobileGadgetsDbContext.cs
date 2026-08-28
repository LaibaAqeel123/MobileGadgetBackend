using Microsoft.EntityFrameworkCore;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class MobileGadgetsDbContext : DbContext
{
    public MobileGadgetsDbContext(DbContextOptions<MobileGadgetsDbContext> options)
        : base(options)
    {
    }

    public DbSet<HeroModel> HeroModels => Set<HeroModel>();
    public DbSet<HeroGeneration> HeroGenerations => Set<HeroGeneration>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();

        modelBuilder.Entity<Scene>().HasData(
            new Scene
            {
                Id = 1,
                Name = "Dark Studio",
                IsDefault = true,
                CamY = 1.15,
                CamZ = -2.6,
                PitchDegrees = 13,
                Focal = 1650,
                LeanDegrees = 10,
                YawDegrees = -22,
                BackgroundTopColor = "#2c2c2f",
                BackgroundBottomColor = "#141416",
                FloorTopColor = "#333336",
                FloorBottomColor = "#111113",
                WallTopColor = "#3d3d40",
                WallBottomColor = "#28282b",
            },
            new Scene
            {
                Id = 2,
                Name = "Light Studio",
                IsDefault = false,
                CamY = 1.15,
                CamZ = -2.6,
                PitchDegrees = 13,
                Focal = 1650,
                LeanDegrees = 10,
                YawDegrees = -22,
                BackgroundTopColor = "#f4f4f2",
                BackgroundBottomColor = "#e2e2de",
                FloorTopColor = "#ffffff",
                FloorBottomColor = "#d6d6d2",
                WallTopColor = "#faf9f7",
                WallBottomColor = "#ebebe8",
            }
        );
    }
}
