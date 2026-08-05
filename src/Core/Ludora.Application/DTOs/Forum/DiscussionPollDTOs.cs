using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public sealed record DiscussionMediaWriteDTO
{
    public int? Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public bool IsSpoiler { get; init; }
}

public sealed record DiscussionPollWriteDTO
{
    public string Question { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
    public DateTime EndsAtUtc { get; init; }
    public PollResultsVisibility ResultsVisibility { get; init; }
    public bool AllowVoteChange { get; init; }
}

public sealed record DiscussionPollVoteRequestDTO
{
    public int OptionId { get; init; }
}

public sealed record DiscussionPollDTO
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public DateTime EndsAtUtc { get; init; }
    public bool IsClosed { get; init; }
    public bool HasEnded { get; init; }
    public bool AllowVoteChange { get; init; }
    public PollResultsVisibility ResultsVisibility { get; init; }
    public bool CanViewResults { get; init; }
    public bool HasVoted { get; init; }
    public int? SelectedOptionId { get; init; }
    public int? TotalVotes { get; init; }
    public bool CanEdit { get; init; }
    public IReadOnlyList<DiscussionPollOptionDTO> Options { get; init; } = [];
    public IReadOnlyList<DiscussionPollParticipantDTO> ParticipantPreview { get; init; } = [];
}

public sealed record DiscussionPollOptionDTO
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public int? VoteCount { get; init; }
    public decimal? Percentage { get; init; }
}

public sealed record DiscussionPollParticipantDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
