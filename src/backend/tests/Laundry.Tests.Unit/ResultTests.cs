using Laundry.BuildingBlocks.Application;
using Xunit;

namespace Laundry.Tests.Unit;

public class ResultTests
{
    private static readonly Error SomeError = new(ProblemTypes.NotFound, "Not found", "No such item");

    [Fact]
    public void SuccessHasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureCarriesError()
    {
        var result = Result.Failure(SomeError);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void FailureWithNoneErrorThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
        Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void SuccessOfTExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureOfTCarriesErrorAndValueThrows()
    {
        var result = Result.Failure<int>(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversionFromValueGivesSuccess()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public void ImplicitConversionFromErrorGivesFailure()
    {
        Result<string> result = SomeError;

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }
}
