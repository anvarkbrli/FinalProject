namespace Ludora.API.Controllers.User
{
    public sealed class ChatGroupUpdateFormRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool RemoveImage { get; set; }
        public List<Guid> MemberUserIdsToAdd { get; set; } = [];
        public IFormFile? Image { get; set; }
    }
}
