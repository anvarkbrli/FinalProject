using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers;

[Route("api/search")]
[ApiController]
[EnableRateLimiting("Search")]
public sealed class SearchController : ControllerBase
{
    private readonly IGlobalSearchService _searchService;

    public SearchController(IGlobalSearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<GlobalSearchResultDTO>> Search(
        [FromQuery] GlobalSearchFilterDTO filter,
        CancellationToken cancellationToken)
    {
        var result = await _searchService.SearchAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }


    [HttpGet("games")]
    public async Task<ActionResult<PagedResultDTO<SearchGameResultDTO>>> SearchGames(
        [FromQuery] SearchPageFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchGamesPageAsync(filter, cancellationToken));
    }

    [HttpGet("users")]
    public async Task<ActionResult<PagedResultDTO<SearchUserResultDTO>>> SearchUsers(
        [FromQuery] SearchPageFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchUsersPageAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("discussions")]
    public async Task<ActionResult<PagedResultDTO<SearchDiscussionResultDTO>>> SearchDiscussions(
        [FromQuery] SearchPageFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchDiscussionsPageAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("reviews")]
    public async Task<ActionResult<PagedResultDTO<SearchReviewResultDTO>>> SearchReviews(
        [FromQuery] SearchPageFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchReviewsPageAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [HttpGet("lists")]
    public async Task<ActionResult<PagedResultDTO<SearchGameListResultDTO>>> SearchLists(
        [FromQuery] SearchPageFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _searchService.SearchListsPageAsync(
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }
}
