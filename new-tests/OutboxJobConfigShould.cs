using Excalibur.Jobs;
using Excalibur.Jobs.Quartz.Outbox;
using Shouldly;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Outbox;

public class OutboxJobConfigShould
{
    [Fact]
    public void InheritFromJobConfig()
    {
        // Act
        var config = new OutboxJobConfig();

        // Assert
        config.ShouldBeAssignableTo<JobConfig>();
    }

    [Fact]
    public void SetAndGetProperties()
    {
        // Arrange
        var config = new OutboxJobConfig
        {
            JobName = "TestOutboxJob",
            JobGroup = "TestGroup",
            Disabled = false,
            CronSchedule = "0 0/5 * * * ?"
        };

        // Assert
        config.JobName.ShouldBe("TestOutboxJob");
        config.JobGroup.ShouldBe("TestGroup");
        config.Disabled.ShouldBeFalse();
        config.CronSchedule.ShouldBe("0 0/5 * * * ?");
    }

    [Fact]
    public void AllowSettingProperties()
    {
        // Arrange
        var config = new OutboxJobConfig();

        // Act
        config.JobName = "OutboxJob";
        config.JobGroup = "Default";
        config.Disabled = true;
        config.CronSchedule = "0 0/15 * * * ?";

        // Assert
        config.JobName.ShouldBe("OutboxJob");
        config.JobGroup.ShouldBe("Default");
        config.Disabled.ShouldBeTrue();
        config.CronSchedule.ShouldBe("0 0/15 * * * ?");
    }
}