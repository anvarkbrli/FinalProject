using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers
{
    [Route("api/companies")]
    [ApiController]
    public sealed class CompaniesController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompaniesController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        [HttpGet]
        public async Task<ActionResult<List<CompanyDetailDTO>>> GetAll(
            [FromQuery] string? searchName)
        {
            var companies = await _companyService.GetAllAsync(searchName);
            return Ok(companies);
        }

        [HttpGet("deleted")]
        public async Task<ActionResult<List<CompanyDetailDTO>>> GetDeleted([FromQuery] string? searchName)
        {
            var companies = await _companyService.GetDeletedAsync(searchName);
            return Ok(companies);
        }

        [HttpGet("{id:int:min(1)}")]
        public async Task<ActionResult<CompanyDetailDTO>> GetById(int id)
        {
            var company = await _companyService.GetByIdAsync(id);
            return Ok(company);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CompanyCreateDTO createDto)
        {
            var companyId = await _companyService.CreateAsync(createDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = companyId },
                new { id = companyId });
        }

        [HttpPatch("{id:int:min(1)}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] CompanyUpdateDTO updateDto)
        {
            await _companyService.UpdateAsync(id, updateDto);
            return NoContent();
        }

        [HttpDelete("{id:int:min(1)}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _companyService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPatch("{id:int:min(1)}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            await _companyService.RestoreAsync(id);
            return NoContent();
        }
    }
}
