using Irony.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using REMS.DTOs;
using REMS.Enititys;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace REMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController(UserService userService, IConfiguration configuration) : ControllerBase
    {
        private readonly UserService _userService = userService;
        private readonly IConfiguration _configuration = configuration;

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Invalid login request.");
            }

            var user = await _userService.LoginAsync(request.Email, request.Password);

            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            var token = CreateToken(user);

            
            var role = GetRole(user); 

            return Ok(new
            {
                Message = "Login successful",
                UserId = user.Id,
                token,
                Role = role 
            });
        }

        // دالة للحصول على الدور
        private string GetRole(User user)
        {
            if (user.IsAdmin) return "Manager";
            if (user.IsItAdmin) return "Admin1";
            if (user.IsFollowUpAdmin) return "Admin";
            if (user.IsFUser) return "FUser";
            return "User";
        }

        [HttpPost("rigester")]
        public async Task<IActionResult> Register([FromBody] RigesterUserDto rigesterUser)
        {
            if (string.IsNullOrEmpty(rigesterUser.Email) || string.IsNullOrEmpty(rigesterUser.Password))
            {
                return BadRequest("Invalid reg request.");
            }

            var user = await _userService.CreateUserAsync(new Enititys.User { FullName = rigesterUser.FullName, Email = rigesterUser.Email, IsAdmin = rigesterUser.IsAdmin, PasswordHash = rigesterUser.Password });

            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            return Ok(new { Message = "reg successful", UserId = user.Id });
        }
        private string CreateToken(User user)
        {
            List<Claim> Claims =
            [
                new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone,user.PhoneNumber),
                new Claim(ClaimTypes.Email,user.Email!),
                new Claim(ClaimTypes.Role,"User")
            ];

            if (user.IsAdmin)
                Claims.Add(new Claim(ClaimTypes.Role, "Manager"));

            if (user.IsItAdmin)
                Claims.Add(new Claim(ClaimTypes.Role, "Admin1"));

            if (user.IsFUser)
                Claims.Add(new Claim(ClaimTypes.Role, "FUser"));

            if (user.IsFollowUpAdmin)
                Claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                _configuration.GetSection("Jwt:Key").Value!));

            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            //to Be Complete
            var token = new JwtSecurityToken(
                claims: Claims,
                signingCredentials: cred,
                expires: DateTime.Now.AddDays(2)
                );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
