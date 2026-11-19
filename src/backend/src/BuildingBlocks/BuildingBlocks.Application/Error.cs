namespace Laundry.BuildingBlocks.Application;

public sealed record Error(
    string Type,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Fields = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(IReadOnlyDictionary<string, IReadOnlyList<string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return new Error(ProblemTypes.Validation, "Validation failed", "One or more fields are invalid.", fields);
    }
}
