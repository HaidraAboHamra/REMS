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
    public class FormDataController(AppDbContext context) : ControllerBase
    {
        private readonly AppDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetFormData()
        {
            var Data = await _context.FormData.ToListAsync();
            return Ok(Data);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFormData(int id)
        {
            var formData = await _context.FormData.FindAsync(id);
            if (formData is null)
                return NotFound($"there no FormData with the id:{id}");

            return Ok(formData);
        }
        [HttpPost]
        public async Task<IActionResult> AddFormData([FromBody] FormData formData)
        {
            try
            {
                _context.FormData.Add(formData);
                await _context.SaveChangesAsync();
                return Ok(formData);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }

        }
        [HttpPut]
        public async Task<IActionResult> EditFormData(FormData formData)
        {
            try
            {
                _context.FormData.Update(formData);
                await _context.SaveChangesAsync();
                return Ok(formData);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFormData(int id)
        {
            try
            {
                var formData = await _context.FormData.FindAsync(id);
                if (formData is null)
                    return NotFound();

                _context.FormData.Remove(formData);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return BadRequest();
            }

        }
    }
}
