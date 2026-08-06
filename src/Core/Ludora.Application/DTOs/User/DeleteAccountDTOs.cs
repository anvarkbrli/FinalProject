using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs
{
    public sealed class DeleteAccountRequestDTO
    {
        [Required(ErrorMessage = "Cari şifrəni daxil edin.")]
        public string CurrentPassword { get; init; } = string.Empty;

        [Required(ErrorMessage = "Hesab silinməsini təsdiqləmək üçün tələb olunan mətni daxil edin.")]
        public string ConfirmationText { get; init; } = string.Empty;
    }

    public sealed class DeleteAccountResultDTO
    {
        public bool DeletedImmediately { get; init; }
        public bool PendingOwnershipTransfers { get; init; }
        public int PendingCommunityCount { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
