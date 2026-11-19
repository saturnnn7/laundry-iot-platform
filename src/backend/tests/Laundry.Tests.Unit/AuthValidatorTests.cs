using Laundry.Identity.Application.Features.Auth.Login;
using Laundry.Identity.Application.Features.Auth.Register;
using Xunit;

namespace Laundry.Tests.Unit;

public class AuthValidatorTests
{
    private static RegisterRequest Valid { get; } =
        new("user@example.com", "0123456789", "Jane Doe", "Resident");

    private static string[] FieldsOf(Laundry.BuildingBlocks.Application.ValidationErrors errors) =>
        errors.Build()?.Fields?.Keys.Order().ToArray() ?? [];

    [Fact]
    public void ValidRequestHasNoErrors()
    {
        Assert.False(RegisterValidator.Validate(Valid).HasErrors);
        Assert.False(RegisterValidator.Validate(Valid with { AccountType = "Admin" }).HasErrors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.example.com")]
    [InlineData("two@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("@example.com")]
    [InlineData("user@localhost")]
    [InlineData("us er@example.com")]
    [InlineData("user@exa mple.com")]
    [InlineData("user\t@example.com")]
    [InlineData("user\u0001@example.com")]
    public void BadEmailIsRejected(string? email)
    {
        var errors = RegisterValidator.Validate(Valid with { Email = email });

        Assert.Equal(["email"], FieldsOf(errors));
    }

    [Fact]
    public void EmailIsTrimmedBeforeTheChecks()
    {
        Assert.False(RegisterValidator.Validate(Valid with { Email = "  user@example.com\n" }).HasErrors);
    }

    [Fact]
    public void EmailLengthLimitIsInclusive()
    {
        var atLimit = new string('a', 254 - "@example.com".Length) + "@example.com";

        Assert.Equal(254, atLimit.Length);
        Assert.False(RegisterValidator.Validate(Valid with { Email = atLimit }).HasErrors);
        Assert.Equal(["email"], FieldsOf(RegisterValidator.Validate(Valid with { Email = "a" + atLimit })));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("short", false)]
    public void PasswordIsRequired(string? password, bool rejected)
    {
        var errors = RegisterValidator.Validate(Valid with { Password = password });

        // The minimum length is enforced by the account store, not here.
        Assert.Equal(rejected, errors.HasErrors);
    }

    [Fact]
    public void PasswordLengthLimitIsInclusive()
    {
        Assert.False(RegisterValidator.Validate(Valid with { Password = new string('p', 128) }).HasErrors);
        Assert.Equal(["password"], FieldsOf(RegisterValidator.Validate(Valid with { Password = new string('p', 129) })));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Jane\nDoe")]
    [InlineData("Jane\u0007")]
    public void BadDisplayNameIsRejected(string? name)
    {
        Assert.Equal(["displayName"], FieldsOf(RegisterValidator.Validate(Valid with { DisplayName = name })));
    }

    [Fact]
    public void DisplayNameLengthIsCountedAfterTrimming()
    {
        Assert.False(RegisterValidator.Validate(Valid with { DisplayName = "  " + new string('n', 100) + "  " }).HasErrors);
        Assert.Equal(
            ["displayName"],
            FieldsOf(RegisterValidator.Validate(Valid with { DisplayName = new string('n', 101) })));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("resident")]
    [InlineData("ADMIN")]
    [InlineData("Technician")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" Admin")]
    [InlineData("Resident,Admin")]
    public void AccountTypeMustBeExactlyAName(string? accountType)
    {
        Assert.Equal(["accountType"], FieldsOf(RegisterValidator.Validate(Valid with { AccountType = accountType })));
    }

    [Fact]
    public void AllFieldErrorsAreReportedTogether()
    {
        var errors = RegisterValidator.Validate(new RegisterRequest("nope", null, " ", "1"));

        Assert.Equal(["accountType", "displayName", "email", "password"], FieldsOf(errors));
    }

    [Theory]
    [InlineData(null, "x", "email")]
    [InlineData("", "x", "email")]
    [InlineData("user@example.com", null, "password")]
    [InlineData("user@example.com", "", "password")]
    public void LoginRequiresBothFields(string? email, string? password, string field)
    {
        Assert.Equal([field], FieldsOf(LoginValidator.Validate(new LoginRequest(email, password))));
    }

    [Fact]
    public void LoginGivesNoFormatHintsAndChecksLimits()
    {
        Assert.False(LoginValidator.Validate(new LoginRequest("not an email", "x")).HasErrors);
        Assert.Equal(["email"], FieldsOf(LoginValidator.Validate(new LoginRequest(new string('e', 255), "x"))));
        Assert.Equal(["password"], FieldsOf(LoginValidator.Validate(new LoginRequest("a@b.c", new string('p', 129)))));
        Assert.False(LoginValidator.Validate(new LoginRequest(new string('e', 254), new string('p', 128))).HasErrors);
    }
}
