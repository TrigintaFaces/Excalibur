using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new DeadLetterOptions();

        options.MaxAttempts.ShouldBe(3);
        options.QueueName.ShouldBe("deadletter");
        options.PreserveMetadata.ShouldBeTrue();
        options.IncludeExceptionDetails.ShouldBeTrue();
        options.EnableRecovery.ShouldBeFalse();
        options.RecoveryInterval.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new DeadLetterOptions
        {
            MaxAttempts = 10,
            QueueName = "custom-dlq",
            PreserveMetadata = false,
            IncludeExceptionDetails = false,
            EnableRecovery = true,
            RecoveryInterval = TimeSpan.FromMinutes(30),
        };

        options.MaxAttempts.ShouldBe(10);
        options.QueueName.ShouldBe("custom-dlq");
        options.PreserveMetadata.ShouldBeFalse();
        options.IncludeExceptionDetails.ShouldBeFalse();
        options.EnableRecovery.ShouldBeTrue();
        options.RecoveryInterval.ShouldBe(TimeSpan.FromMinutes(30));
    }
}
