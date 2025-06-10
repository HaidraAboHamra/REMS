using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;

namespace REMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeeController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetEmployee()
        {
            var employees = await _context.Employees.ToListAsync();
            return Ok(employees);
        }
        [HttpGet("{Id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {      
            var employee = await _context.Employees.FindAsync(id);
            if (employee is null)
                return NotFound($"there no Employee with the id:{id}");

            return Ok(employee);
        }
        [HttpPost]
        public async Task<IActionResult> AddEmployee([FromBody] Employees employee)
        {
            try
            {
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                return Ok(employee);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }
        [HttpPut]
        public async Task<IActionResult> EditEmployee([FromBody] Employees employees)
        {
            try
            {
                _context.Employees.Update(employees);
                await _context.SaveChangesAsync();

                return Ok(employees);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);
                if (employee is null)
                    return NotFound($"there no Employee with the id:{id}");

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();

                return Ok(employee);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }
    }
}
