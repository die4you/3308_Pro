using MebKoordinatorluk.Core.Common;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class ValidationHelperTests
{
    [Theory]
    [InlineData("10000000146", true)]  // Valid algorithmic TC
    [InlineData("22222222220", true)]  // Valid algorithmic TC
    [InlineData("12345678901", false)] // Invalid checksum
    [InlineData("01234567890", false)] // Starts with 0
    [InlineData("12345", false)]       // Too short
    [InlineData("123456789012", false)]// Too long
    [InlineData("", false)]            // Empty
    [InlineData("abcdefghijk", false)] // Non-digits
    public void IsValidTcKimlikNo_ShouldValidateCorrectly(string? tc, bool expected)
    {
        var result = ValidationHelper.IsValidTcKimlikNo(tc);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("05551112233", true)]
    [InlineData("5551112233", true)]
    [InlineData("123", false)]
    [InlineData("", true)] // Optional
    public void IsValidPhone_ShouldValidateCorrectly(string? phone, bool expected)
    {
        var result = ValidationHelper.IsValidPhone(phone);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ogretmen@meb.k12.tr", true)]
    [InlineData("info@firma.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", true)] // Optional
    public void IsValidEmail_ShouldValidateCorrectly(string? email, bool expected)
    {
        var result = ValidationHelper.IsValidEmail(email);
        Assert.Equal(expected, result);
    }
}
