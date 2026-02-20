using Excalibur.Dispatch.AuditLogging.Retention;

namespace Excalibur.Dispatch.AuditLogging.Tests.Retention;

public class AuditRetentionPolicyShould
{
    [Fact]
    public void Create_policy_with_required_properties()
    {
        var policy = new AuditRetentionPolicy
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(1),
            BatchSize = 1000,
            ArchiveBeforeDelete = true
        };

        policy.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
        policy.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
        policy.BatchSize.ShouldBe(1000);
        policy.ArchiveBeforeDelete.ShouldBeTrue();
    }

    [Fact]
    public void Support_record_equality()
    {
        var policy1 = new AuditRetentionPolicy
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(1),
            BatchSize = 1000,
            ArchiveBeforeDelete = false
        };

        var policy2 = new AuditRetentionPolicy
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(1),
            BatchSize = 1000,
            ArchiveBeforeDelete = false
        };

        policy1.ShouldBe(policy2);
    }

    [Fact]
    public void Support_with_expression()
    {
        var original = new AuditRetentionPolicy
        {
            RetentionPeriod = TimeSpan.FromDays(365),
            CleanupInterval = TimeSpan.FromHours(1),
            BatchSize = 1000,
            ArchiveBeforeDelete = false
        };

        var modified = original with { ArchiveBeforeDelete = true };

        modified.ArchiveBeforeDelete.ShouldBeTrue();
        modified.RetentionPeriod.ShouldBe(original.RetentionPeriod);
    }
}
