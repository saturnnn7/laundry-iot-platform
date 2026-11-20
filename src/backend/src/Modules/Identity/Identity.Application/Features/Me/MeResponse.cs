namespace Laundry.Identity.Application.Features.Me;

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string AccountType,
    IReadOnlyList<MembershipResponse> Memberships);

// Role is Resident, Technician or Admin.
public sealed record MembershipResponse(Guid LocationId, string LocationName, string Role);
