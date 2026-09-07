using System.ComponentModel.DataAnnotations;

namespace REMS.DTOs;

public class EmployeeCreateRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Range(16, 100)]
    public int Age { get; set; }

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [StringLength(150)]
    public string Major { get; set; } = string.Empty;
}

public sealed class EmployeeUpdateRequest : EmployeeCreateRequest
{
    [Required]
    public int Id { get; set; }
}
