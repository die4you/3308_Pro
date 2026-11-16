using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class CoordinationCalculatorTests
{
    private readonly MebCoordinationCalculator _calculator = new();

    [Theory]
    [InlineData(0, DistanceType.MucavirAlanIci, 0.0)]
    [InlineData(5, DistanceType.MucavirAlanIci, 4.0)]
    [InlineData(15, DistanceType.MucavirAlanIci, 4.0)]
    [InlineData(18, DistanceType.MucavirAlanIci, 5.0)]
    [InlineData(20, DistanceType.MucavirAlanIci, 5.0)]
    [InlineData(24, DistanceType.MucavirAlanIci, 6.0)]
    [InlineData(30, DistanceType.MucavirAlanIci, 7.0)]
    [InlineData(35, DistanceType.MucavirAlanIci, 8.0)]
    public void CalculateWeeklyHours_MucavirAlanIci_ShouldReturnExpectedHours(int studentCount, DistanceType distanceType, double expectedHours)
    {
        // Act
        var result = _calculator.CalculateWeeklyHours(studentCount, distanceType);

        // Assert
        Assert.Equal(expectedHours, result);
    }

    [Theory]
    [InlineData(10, DistanceType.MucavirAlanDisi, 6.0)] // 4.0 + 2.0
    [InlineData(18, DistanceType.MucavirAlanDisi, 7.0)] // 5.0 + 2.0
    [InlineData(25, DistanceType.MucavirAlanDisi, 8.0)] // 6.0 + 2.0
    public void CalculateWeeklyHours_MucavirAlanDisi_ShouldIncludeAdditionalHours(int studentCount, DistanceType distanceType, double expectedHours)
    {
        // Act
        var result = _calculator.CalculateWeeklyHours(studentCount, distanceType);

        // Assert
        Assert.Equal(expectedHours, result);
    }

    [Fact]
    public void CanAssignHours_ShouldRespectMaxWeeklyLimit()
    {
        // 12 saat mevcut + 4 saat = 16 (Limit: 16) -> Geçerli
        Assert.True(_calculator.CanAssignHours(12.0, 4.0, 16));

        // 14 saat mevcut + 4 saat = 18 (Limit: 16) -> Aşıldı, geçersiz
        Assert.False(_calculator.CanAssignHours(14.0, 4.0, 16));
    }
}
