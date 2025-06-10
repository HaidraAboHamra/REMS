using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REMS.Data;
using REMS.Enititys;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace REMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GetRoleController : ControllerBase
    {
     
        private readonly UserService _userService;
        private readonly AppDbContext _db;

        public GetRoleController(UserService userService,AppDbContext appDbContext)
        {
        _userService = userService;
            _db = appDbContext;
        
        }
        // POST api/<GetRole>ِ
        [HttpGet("{id}")]

        public async Task<IActionResult> GetRoleById(int id)
        {
            var user1 = await _userService.GetById(id);

            if (user1.IsFailure)
            {
                return Unauthorized("Invalid email or password.");
            }
            var user = user1.Value!;
            var role = GetRole(user);
            return Ok(new
            {
                Message = "Login successful",
                UserName = user.FullName,
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
        public class LoginRequest1
        {
            public int Id { get; set; }
        }
    }
}
