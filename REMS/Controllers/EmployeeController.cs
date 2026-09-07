using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.DTOs;

namespace REMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin,Admin1")]
    [EnableRateLimiting("api")]
    public class EmployeeController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetEmployee(CancellationToken cancellationToken)
        {
            var employees = await _context.Employees.AsNoTracking().ToListAsync(cancellationToken);
            return Ok(employees);
        }
        [HttpGet("{Id}")]
        public async Task<IActionResult> GetEmployee(int id, CancellationToken cancellationToken)
        {      
            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (employee is null)
                return NotFound($"there no Employee with the id:{id}");

            return Ok(employee);
        }
        [HttpPost]
        public async Task<IActionResult> AddEmployee([FromBody] EmployeeCreateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var employee = new Employees { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Age = request.Age, Email = request.Email.Trim(), Phone = request.Phone.Trim(), Address = request.Address.Trim(), Major = request.Major.Trim() };
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(employee);
            }
            catch (DbUpdateException)
            {
                return Conflict("Employee could not be saved.");
            }
        }
        [HttpPut]
        public async Task<IActionResult> EditEmployee([FromBody] EmployeeUpdateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
                if (employee is null) return NotFound();
                employee.FirstName = request.FirstName.Trim(); employee.LastName = request.LastName.Trim(); employee.Age = request.Age;
                employee.Email = request.Email.Trim(); employee.Phone = request.Phone.Trim(); employee.Address = request.Address.Trim(); employee.Major = request.Major.Trim();
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(employee);
            }
            catch (DbUpdateException)
            {
                return Conflict("Employee could not be updated.");
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (employee is null)
                    return NotFound($"there no Employee with the id:{id}");

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(employee);
            }
            catch (DbUpdateException)
            {
                return Conflict("Employee could not be deleted.");
            }
        }
    }
}
