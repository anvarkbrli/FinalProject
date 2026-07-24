namespace Ludora.Domain.Enums;

public enum ChatRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

public enum ChatMessageKind
{
    Text = 0,
    Image = 1,
    Video = 2,
    GameRecommendation = 3
}

public enum ChatMessageDeliveryStatus
{
    Sent = 0,
    Delivered = 1,
    Read = 2
}
