namespace Ludora.Domain.Enums;

public enum ModeratorActionType
{
    ReportStatusChanged = 1,
    WarningIssued = 2,
    TemporaryBanIssued = 3,
    PermanentBanIssued = 4,
    UserUnbanned = 5,
    RoleChanged = 6,
    AdministrativeAction = 7
}
