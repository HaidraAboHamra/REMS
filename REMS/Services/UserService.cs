using Microsoft.AspNetCore.Identity;
using REMS.Enititys;
using REMS.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using REMS.Abstractions;
using Microsoft.AspNetCore.Authorization;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;

    public UserService(AppDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<User> CreateUserAsync(User user)
    {
          user.PasswordHash = _passwordHasher.HashPassword(user, user.PasswordHash);
        _context.Users.Add(user);
        await _context.SaveChangesAsync(); 

        return user;
    }

    public async Task<Result<User>> GetById(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return Result<User>.Failure(new Error("Can't find a user with this id"));
        }
        return Result<User>.Success(user);
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return result == PasswordVerificationResult.Success ? user : null;
    }

	public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
	{
		var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
		if (user == null)
		{
			return Result.Failure(new Error("User not found"));
		}

		var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
		if (passwordVerificationResult != PasswordVerificationResult.Success)
		{
			return Result.Failure(new Error("Current password is incorrect"));
		}

		user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
		_context.Users.Update(user);
		await _context.SaveChangesAsync();

		return Result.Success();
	}

}
