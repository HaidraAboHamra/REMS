namespace REMS.DTOs;

public class RigesterUserDto
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public bool IsAdmin { get; set; } = false;
}
