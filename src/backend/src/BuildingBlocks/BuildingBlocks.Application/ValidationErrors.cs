namespace Laundry.BuildingBlocks.Application;

public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _fields = new(StringComparer.Ordinal);

    public bool HasErrors => _fields.Count > 0;

    public ValidationErrors Add(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!_fields.TryGetValue(field, out var messages))
        {
            messages = [];
            _fields.Add(field, messages);
        }

        messages.Add(message);

        return this;
    }

    // Returns null when nothing was added.
    public Error? Build() =>
        HasErrors
            ? Error.Validation(_fields.ToDictionary(f => f.Key, f => (IReadOnlyList<string>)f.Value.ToArray(), StringComparer.Ordinal))
            : null;
}
