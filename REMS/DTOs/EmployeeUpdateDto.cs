using System.ComponentModel.DataAnnotations;

namespace REMS.DTOs;

public sealed class EmployeeUpdateDto
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    public string? PhoneNumber { get; set; }

    [StringLength(80)]
    public string? TelegramUsername { get; set; }

    [StringLength(128, MinimumLength = 8)]
    public string? NewPassword { get; set; }

    public bool IsFUser { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsFollowUpAdmin { get; set; }
    public bool IsItAdmin { get; set; }
}