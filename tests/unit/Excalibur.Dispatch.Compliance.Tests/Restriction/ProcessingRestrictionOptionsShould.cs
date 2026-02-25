using Excalibur.Dispatch.Compliance.Restriction;

namespace Excalibur.Dispatch.Compliance.Tests.Restriction;

public class ProcessingRestrictionOptionsShould
{
    [Fact]
    public void Have_30_day_default_restriction_duration()
    {
        var options = new ProcessingRestrictionOptions();

        options.DefaultRestrictionDuration.ShouldBe(TimeSpan.FromDays(30));
    }

    [Fact]
    public void Have_notify_on_restriction_enabled_by_default()
    {
        var options = new ProcessingRestrictionOptions();

        options.NotifyOnRestriction.ShouldBeTrue();
    }
}
