using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyDetailDTO>> GetAllAsync(
        string? searchName,
        CatalogSource? source = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyDetailDTO>> GetDeletedAsync(
        CancellationToken cancellationToken = default);

    Task<CompanyDetailDTO> GetByIdAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);

    Task<long> CreateAsync(
        CompanyCreateDTO createDto,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        CatalogSource source,
        long id,
        CompanyUpdateDTO updateDto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        CatalogSource source,
        long id,
        CancellationToken cancellationToken = default);
}
