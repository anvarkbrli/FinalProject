using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Contact;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IContactMessageService
    {
        Task<ContactMessageDTO> CreateAsync(
            Guid? userId,
            ContactMessageCreateDTO dto,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<ContactMessageDTO>> GetAllForAdminAsync(
            AdminContactMessageFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<ContactMessageDTO> GetByIdForAdminAsync(
            int contactMessageId,
            CancellationToken cancellationToken = default);

        Task<ContactMessageDTO> UpdateStatusAsync(
            int contactMessageId,
            ContactMessageStatus status,
            CancellationToken cancellationToken = default);

        Task<ContactMessageDTO> ReplyAsync(
            int contactMessageId,
            Guid adminUserId,
            ContactMessageReplyDTO dto,
            CancellationToken cancellationToken = default);
    }
}
