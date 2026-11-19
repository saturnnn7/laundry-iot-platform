using Laundry.BuildingBlocks.Application;
using Xunit;

namespace Laundry.Tests.Unit;

public class ValidationErrorsTests
{
    [Fact]
    public void ErrorWithoutFieldsKeepsExistingShape()
    {
        var error = new Error(ProblemTypes.NotFound, "Not found", "No such item");

        Assert.Null(error.Fields);
        Assert.Equal(new Error(ProblemTypes.NotFound, "Not found", "No such item"), error);
    }

    [Fact]
    public void ErrorCanCarryFields()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>> { ["name"] = ["required"] };

        var error = Error.Validation(fields);

        Assert.Equal(ProblemTypes.Validation, error.Type);
        Assert.Same(fields, error.Fields);
    }

    [Fact]
    public void ValidationFactoryRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Error.Validation(null!));
    }

    [Fact]
    public void EmptyBuilderReportsNoErrors()
    {
        var errors = new ValidationErrors();

        Assert.False(errors.HasErrors);
        Assert.Null(errors.Build());
    }

    [Fact]
    public void BuilderGroupsMessagesByFieldInOrder()
    {
        var errors = new ValidationErrors()
            .Add("name", "required")
            .Add("count", "too big")
            .Add("name", "too long");

        var error = errors.Build();

        Assert.True(errors.HasErrors);
        Assert.NotNull(error);
        Assert.Equal(ProblemTypes.Validation, error.Type);
        Assert.NotNull(error.Fields);
        Assert.Equal(2, error.Fields.Count);
        Assert.Equal(["required", "too long"], error.Fields["name"]);
        Assert.Equal(["too big"], error.Fields["count"]);
    }

    [Fact]
    public void BuiltErrorIsNotChangedByLaterAdds()
    {
        var errors = new ValidationErrors().Add("name", "required");
        var error = errors.Build();

        errors.Add("name", "other");

        Assert.Equal(["required"], error!.Fields!["name"]);
    }

    [Theory]
    [InlineData("", "message")]
    [InlineData("field", " ")]
    public void BuilderRejectsBlankInput(string field, string message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ValidationErrors().Add(field, message));
    }
}
