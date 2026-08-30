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
            // Pose (CamY/CamZ/PitchDegrees/Focal/LeanDegrees/YawDegrees) matches the approved
            // "room_polish_v1" prototype exactly — a near-upright resting pose, not the more
            // dramatic diagonal lean tried and explicitly rejected earlier. Dark Studio's colors
            // match that same prototype's warm dark palette; Light Studio keeps its own light
            // palette but shares the same camera/pose since that's a geometry decision, not a
            // theme decision.
            new Scene
            {
                Id = 1,
                Name = "Dark Studio",
                IsDefault = true,
                CamY = 1.35,
                CamZ = -2.1,
                PitchDegrees = 9,
                Focal = 1500,
                LeanDegrees = 5,
                YawDegrees = 0,
                BackgroundTopColor = "#5a5654",
                BackgroundBottomColor = "#0c0b0b",
                FloorTopColor = "#463e3a",
                FloorBottomColor = "#080707",
                WallTopColor = "#686360",
                WallBottomColor = "#242221",
            },
            new Scene
            {
                Id = 2,
                Name = "Light Studio",
                IsDefault = false,
                CamY = 1.35,
                CamZ = -2.1,
                PitchDegrees = 9,
                Focal = 1500,
                LeanDegrees = 5,
                YawDegrees = 0,
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
