using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REMS.Data;
using REMS.Enititys;
using REMS.DTOs;
using Microsoft.AspNetCore.RateLimiting;

namespace REMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin,Admin1")]
    [EnableRateLimiting("api")]
    public class FormDataController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetFormData(CancellationToken cancellationToken)
        {
            var Data = await _context.FormData.AsNoTracking().ToListAsync(cancellationToken);
            return Ok(Data);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFormData(int id, CancellationToken cancellationToken)
        {
            var formData = await _context.FormData.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (formData is null)
                return NotFound($"there no FormData with the id:{id}");

            return Ok(formData);
        }
        [HttpPost]
        public async Task<IActionResult> AddFormData([FromBody] FormDataRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var formData = ToEntity(request);
                _context.FormData.Add(formData);
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(formData);
            }
            catch (DbUpdateException)
            {
                return Conflict("Form data could not be saved.");
            }

        }
        [HttpPut]
        public async Task<IActionResult> EditFormData([FromBody] FormDataUpdateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var formData = await _context.FormData.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
                if (formData is null) return NotFound();
                Apply(formData, request);
                await _context.SaveChangesAsync(cancellationToken);
                return Ok(formData);
            }
            catch (DbUpdateException)
            {
                return Conflict("Form data could not be updated.");
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFormData(int id, CancellationToken cancellationToken)
        {
            try
            {
                var formData = await _context.FormData.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (formData is null)
                    return NotFound();

                _context.FormData.Remove(formData);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok();
            }
            catch (DbUpdateException)
            {
                return Conflict("Form data could not be deleted.");
            }

        }

        private static FormData ToEntity(FormDataRequest request)
        {
            var entity = new FormData();
            Apply(entity, request);
            return entity;
        }

        private static void Apply(FormData entity, FormDataRequest request)
        {
            entity.FirstName = request.FirstName?.Trim(); entity.LastName = request.LastName?.Trim(); entity.Age = request.Age;
            entity.Email = request.Email?.Trim(); entity.Phone = request.Phone?.Trim(); entity.Address = request.Address?.Trim();
            entity.FamiliarLanguages = request.FamiliarLanguages?.Trim(); entity.ProficientLanguages = request.ProficientLanguages?.Trim();
            entity.LearningProblems = request.LearningProblems?.Trim(); entity.Domain = request.Domain?.Trim(); entity.Major = request.Major?.Trim();
            entity.AcademicYear = request.AcademicYear?.Trim(); entity.Description = request.Description?.Trim(); entity.ExpectedGradutionYear = request.ExpectedGradutionYear?.Trim();
            entity.ProgrammingAbility = request.ProgrammingAbility; entity.TeamWorkAbility = request.TeamWorkAbility;
            entity.IndividualTasksAbility = request.IndividualTasksAbility; entity.CleanCodeAbility = request.CleanCodeAbility;
        }
    }
}
