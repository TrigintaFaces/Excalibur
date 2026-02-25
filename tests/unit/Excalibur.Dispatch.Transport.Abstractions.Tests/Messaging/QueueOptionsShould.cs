using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class QueueOptionsShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new QueueOptions();

        // Assert
        sut.MaxSizeInMB.ShouldBeNull();
        sut.DefaultMessageTimeToLive.ShouldBeNull();
        sut.LockDuration.ShouldBeNull();
        sut.EnableDeduplication.ShouldBeNull();
        sut.DuplicateDetectionWindow.ShouldBeNull();
        sut.RequiresSession.ShouldBeNull();
        sut.DeadLetteringOnMessageExpiration.ShouldBeNull();
        sut.MaxDeliveryCount.ShouldBeNull();
        sut.EnablePartitioning.ShouldBeNull();
        sut.Properties.ShouldNotBeNull();
        sut.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange & Act
        var sut = new QueueOptions
        {
            MaxSizeInMB = 5120,
            DefaultMessageTimeToLive = TimeSpan.FromHours(24),
            LockDuration = TimeSpan.FromMinutes(5),
            EnableDeduplication = true,
            DuplicateDetectionWindow = TimeSpan.FromMinutes(10),
            RequiresSession = true,
            DeadLetteringOnMessageExpiration = true,
            MaxDeliveryCount = 10,
            EnablePartitioning = true,
            Properties = { ["custom"] = "value" }
        };

        // Assert
        sut.MaxSizeInMB.ShouldBe(5120);
        sut.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromHours(24));
        sut.LockDuration.ShouldBe(TimeSpan.FromMinutes(5));
        sut.EnableDeduplication.ShouldBe(true);
        sut.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(10));
        sut.RequiresSession.ShouldBe(true);
        sut.DeadLetteringOnMessageExpiration.ShouldBe(true);
        sut.MaxDeliveryCount.ShouldBe(10);
        sut.EnablePartitioning.ShouldBe(true);
        sut.Properties.Count.ShouldBe(1);
    }
}
