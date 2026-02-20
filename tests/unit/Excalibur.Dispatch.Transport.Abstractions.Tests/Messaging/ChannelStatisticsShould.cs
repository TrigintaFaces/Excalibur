using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ChannelStatisticsShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new ChannelStatistics();

        // Assert
        sut.CurrentCount.ShouldBe(0);
        sut.Capacity.ShouldBe(0);
        sut.TotalWritten.ShouldBe(0);
        sut.TotalRead.ShouldBe(0);
        sut.FullCount.ShouldBe(0);
        sut.IsWriterCompleted.ShouldBeFalse();
    }

    [Fact]
    public void Calculate_utilization_percentage_when_capacity_is_set()
    {
        // Arrange
        var sut = new ChannelStatistics
        {
            CurrentCount = 50,
            Capacity = 100
        };

        // Act & Assert
        sut.UtilizationPercentage.ShouldBe(50.0);
    }

    [Fact]
    public void Return_zero_utilization_when_capacity_is_zero()
    {
        // Arrange
        var sut = new ChannelStatistics
        {
            CurrentCount = 10,
            Capacity = 0
        };

        // Act & Assert
        sut.UtilizationPercentage.ShouldBe(0.0);
    }

    [Fact]
    public void Calculate_full_utilization()
    {
        // Arrange
        var sut = new ChannelStatistics
        {
            CurrentCount = 200,
            Capacity = 200
        };

        // Act & Assert
        sut.UtilizationPercentage.ShouldBe(100.0);
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Act
        var sut = new ChannelStatistics
        {
            CurrentCount = 75,
            Capacity = 150,
            TotalWritten = 10000,
            TotalRead = 9925,
            FullCount = 5,
            IsWriterCompleted = true
        };

        // Assert
        sut.CurrentCount.ShouldBe(75);
        sut.Capacity.ShouldBe(150);
        sut.TotalWritten.ShouldBe(10000);
        sut.TotalRead.ShouldBe(9925);
        sut.FullCount.ShouldBe(5);
        sut.IsWriterCompleted.ShouldBeTrue();
        sut.UtilizationPercentage.ShouldBe(50.0);
    }
}
