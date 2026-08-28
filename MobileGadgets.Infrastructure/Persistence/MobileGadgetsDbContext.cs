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
}
