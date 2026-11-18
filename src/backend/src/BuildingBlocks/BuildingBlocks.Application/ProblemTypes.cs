namespace Laundry.BuildingBlocks.Application;

public static class ProblemTypes
{
    public const string Validation = "validation";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not-found";
    public const string NotMember = "not-member";
    public const string InviteInvalid = "invite-invalid";
    public const string SlotConflict = "slot-conflict";
    public const string LeadTime = "lead-time";
    public const string Horizon = "horizon";
    public const string MachineUnavailable = "machine-unavailable";
    public const string MachineBusy = "machine-busy";
    public const string HoldLimit = "hold-limit";
    public const string CodeInvalid = "code-invalid";
    public const string CodeExpired = "code-expired";
    public const string TooManyAttempts = "too-many-attempts";
    public const string ProgramNotFit = "program-not-fit";
    public const string CommandFailed = "command-failed";
    public const string IdempotencyConflict = "idempotency-conflict";
}
