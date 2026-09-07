using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using REMS.Abstractions;
using REMS.Data;
using REMS.Enititys;
using System.Security.Cryptography;
using System.Threading.Tasks;
using REMS.DTOs;
using REMS.Services;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly AuditLogService _audit;
    private readonly ProfileImageService _profileImages;

    public UserService(AppDbContext context, AuditLogService audit, ProfileImageService profileImages)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<User>();
        _audit = audit;
        _profileImages = profileImages;
    }

    public async Task<bool> UpdateEmployeeImageAsync(int actorId, int employeeId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Users.FindAsync([actorId], cancellationToken);
        var employee = await _context.Users.FirstOrDefaultAsync(x => x.Id == employeeId && x.IsFUser, cancellationToken);
        if (actor is null || employee is null || (!actor.IsFollowUpAdmin && !actor.IsAdmin)) return false;

        var saved = await _profileImages.SaveAsync(file, employeeId, cancellationToken);
        employee.ProfileImagePath = saved.RelativePath;
        employee.ProfileImageContentType = saved.ContentType;
        await _context.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ProfileImageUpdated", "تم تحديث الصورة الشخصية.", employeeId, "User", employeeId.ToString(), cancellationToken);
        return true;
    }

    public async Task<User?> UpdateEmployeeAsync(int actorId, int employeeId, EmployeeUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Users.FindAsync([actorId], cancellationToken);
        var employee = await _context.Users.FirstOrDefaultAsync(x => x.Id == employeeId && x.IsFUser, cancellationToken);
        if (actor is null || employee is null || (!actor.IsFollowUpAdmin && !actor.IsAdmin)) return null;

        employee.FullName = dto.FullName.Trim();
        employee.Email = dto.Email.Trim();
        employee.PhoneNumber = dto.PhoneNumber?.Trim();
        employee.TelegramUsername = NormalizeTelegramUsername(dto.TelegramUsername);
        employee.IsFUser = dto.IsFUser;
        employee.IsAdmin = dto.IsAdmin;
        employee.IsFollowUpAdmin = dto.IsFollowUpAdmin;
        employee.IsItAdmin = dto.IsItAdmin;
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            employee.PasswordHash = _passwordHasher.HashPassword(employee, dto.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("EmployeeUpdated", $"تم تحديث بيانات الموظف {employee.Id} وصلاحياته.", employee.Id, "User", employee.Id.ToString(), cancellationToken);
        return employee;
    }

    public async Task<User> CreateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PasswordHash = _passwordHasher.HashPassword(user, user.PasswordHash);
        user.TelegramUsername = NormalizeTelegramUsername(user.TelegramUsername);
        user.TelegramLinkToken ??= CreateTelegramLinkToken();
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<Result<User>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return Result<User>.Failure(new Error("Can't find a user with this id"));
        }
        return Result<User>.Success(user);
    }
    public async Task<Result<List<User>>> GetAll()
    {
        var users = await _context.Users.AsNoTracking().ToListAsync();

        if (users == null || users.Count == 0)
        {
            return Result<List<User>>.Failure(new Error("لا يوجد مستخدمون في النظام."));
        }

        return Result<List<User>>.Success(users);

    }
    public async Task<User?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email.Trim(), cancellationToken);

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

    public async Task<string> CreateTelegramLinkTokenAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.TelegramLinkToken = CreateTelegramLinkToken();
        user.ChatId = null;
        user.TelegramLinkedAt = null;
        await _context.SaveChangesAsync(cancellationToken);
        return user.TelegramLinkToken;
    }

    private static string CreateTelegramLinkToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

    private static string? NormalizeTelegramUsername(string? username)
    {
        var value = username?.Trim().TrimStart('@');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

}
