namespace Ludora.API.Controllers.User
{
    public sealed class ChatGroupCreateFormRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<Guid> MemberUserIds { get; set; } = [];
        public IFormFile? Image { get; set; }
    }
}
