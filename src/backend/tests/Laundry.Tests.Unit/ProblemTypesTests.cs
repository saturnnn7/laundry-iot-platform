using System.Reflection;
using Laundry.BuildingBlocks.Application;
using Xunit;

namespace Laundry.Tests.Unit;

public class ProblemTypesTests
{
    private static string[] Values() =>
        typeof(ProblemTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void HasEighteenSlugs()
    {
        Assert.Equal(18, Values().Length);
    }

    [Fact]
    public void EverySlugIsLowerKebabCase()
    {
        foreach (var value in Values())
        {
            Assert.Matches("^[a-z]+(-[a-z]+)*$", value);
        }
    }

    [Fact]
    public void SlugsAreUnique()
    {
        var values = Values();

        Assert.Equal(values.Length, values.Distinct().Count());
    }
}
