using Xunit;

namespace Laundry.Tests.Architecture;

internal static class Assertions
{
    public static void NoViolations(IEnumerable<string> violations)
    {
        var list = violations.ToList();

        Assert.True(list.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, list));
    }
}
