using Excalibur.Dispatch.AuditLogging.Retention;

namespace Excalibur.Dispatch.AuditLogging.Tests.Retention;

public class AuditRetentionOptionsShould
{
    [Fact]
    public void Default_retention_period_to_seven_years()
    {
        var options = new AuditRetentionOptions();

        options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
    }

    [Fact]
    public void Default_cleanup_interval_to_one_day()
    {
        var options = new AuditRetentionOptions();

        options.CleanupInterval.ShouldBe(TimeSpan.FromDays(1));
    }

    [Fact]
    public void Default_batch_size_to_10000()
    {
        var options = new AuditRetentionOptions();

        options.BatchSize.ShouldBe(10000);
    }

    [Fact]
    public void Default_archive_before_delete_to_false()
    {
        var options = new AuditRetentionOptions();

        options.ArchiveBeforeDelete.ShouldBeFalse();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        var options = new AuditRetentionOptions
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(6),
            BatchSize = 5000,
            ArchiveBeforeDelete = true
        };

        options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
        options.BatchSize.ShouldBe(5000);
        options.ArchiveBeforeDelete.ShouldBeTrue();
    }
}
