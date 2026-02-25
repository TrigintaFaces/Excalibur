using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ScheduleInfoShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new ScheduleInfo();

        // Assert
        sut.ScheduleId.ShouldBe(string.Empty);
        sut.Message.ShouldBeNull();
        sut.ScheduledTime.ShouldBe(default);
        sut.CreatedTime.ShouldBe(default);
        sut.Status.ShouldBe(ScheduleStatus.Scheduled);
        sut.LastError.ShouldBeNull();
        sut.DeliveryAttempts.ShouldBe(0);
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange
        var scheduled = DateTimeOffset.UtcNow.AddHours(1);
        var created = DateTimeOffset.UtcNow;
        var message = A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>();

        // Act
        var sut = new ScheduleInfo
        {
            ScheduleId = "sched-001",
            Message = message,
            ScheduledTime = scheduled,
            CreatedTime = created,
            Status = ScheduleStatus.Completed,
            LastError = "Timeout occurred",
            DeliveryAttempts = 3
        };

        // Assert
        sut.ScheduleId.ShouldBe("sched-001");
        sut.Message.ShouldNotBeNull();
        sut.ScheduledTime.ShouldBe(scheduled);
        sut.CreatedTime.ShouldBe(created);
        sut.Status.ShouldBe(ScheduleStatus.Completed);
        sut.LastError.ShouldBe("Timeout occurred");
        sut.DeliveryAttempts.ShouldBe(3);
    }

    [Theory]
    [InlineData(ScheduleStatus.Scheduled)]
    [InlineData(ScheduleStatus.Completed)]
    [InlineData(ScheduleStatus.Failed)]
    [InlineData(ScheduleStatus.Cancelled)]
    public void Support_all_schedule_statuses(ScheduleStatus status)
    {
        // Arrange & Act
        var sut = new ScheduleInfo { Status = status };

        // Assert
        sut.Status.ShouldBe(status);
    }
}
