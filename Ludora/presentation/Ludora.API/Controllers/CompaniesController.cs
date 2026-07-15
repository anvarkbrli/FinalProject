using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

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
    public async Task<ActionResult<IReadOnlyList<CompanyDetailDTO>>> GetAll(
        [FromQuery] string? searchName,
        [FromQuery] CatalogSource? source = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _companyService.GetAllAsync(searchName, source, cancellationToken));
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<IReadOnlyList<CompanyDetailDTO>>> GetDeleted(
        CancellationToken cancellationToken)
    {
        return Ok(await _companyService.GetDeletedAsync(cancellationToken));
    }

    [HttpGet("{source}/{id:long:min(1)}")]
    public async Task<ActionResult<CompanyDetailDTO>> GetById(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        return Ok(await _companyService.GetByIdAsync(source, id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CompanyCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var id = await _companyService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { source = CatalogSource.Local, id },
            new { source = CatalogSource.Local, id });
    }

    [HttpPatch("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Update(
        CatalogSource source,
        long id,
        [FromBody] CompanyUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        await _companyService.UpdateAsync(source, id, dto, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{source}/{id:long:min(1)}")]
    public async Task<IActionResult> Delete(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _companyService.DeleteAsync(source, id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{source}/{id:long:min(1)}/restore")]
    public async Task<IActionResult> Restore(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken)
    {
        await _companyService.RestoreAsync(source, id, cancellationToken);
        return NoContent();
    }
}
