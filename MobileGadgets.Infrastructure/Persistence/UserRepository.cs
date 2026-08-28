using Microsoft.EntityFrameworkCore;
using MobileGadgets.Application.Interfaces;
using MobileGadgets.Domain;

namespace MobileGadgets.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly MobileGadgetsDbContext _db;

    public UserRepository(MobileGadgetsDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByIdAsync(int id) =>
        await _db.Users.FindAsync(id);

    public async Task<List<User>> GetAllAsync() =>
        await _db.Users.OrderBy(u => u.Email).ToListAsync();

    public async Task AddAsync(User user) =>
        await _db.Users.AddAsync(user);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
