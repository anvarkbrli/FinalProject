using System.Security.Claims;
using Ludora.API.Security;
using Ludora.Application.DTOs.Contact;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers;

[ApiController]
[Route("api/contact-messages")]
public sealed class ContactMessagesController : ControllerBase
{
    private readonly IContactMessageService _contactMessageService;

    public ContactMessagesController(IContactMessageService contactMessageService)
    {
        _contactMessageService = contactMessageService;
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicyNames.Contact)]
    public async Task<ActionResult<ContactMessageReceiptDTO>> Create(
        [FromBody] ContactMessageCreateDTO dto,
        CancellationToken cancellationToken)
    {
        Guid? userId = null;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdValue, out var parsedUserId))
            userId = parsedUserId;

        var result = await _contactMessageService.CreateAsync(
            userId,
            dto,
            cancellationToken);

        return Created(
            $"/api/contact-messages/{result.Id}",
            new ContactMessageReceiptDTO
            {
                Id = result.Id,
                Status = result.Status,
                CreatedAtUtc = result.CreatedAtUtc
            });
    }
}
