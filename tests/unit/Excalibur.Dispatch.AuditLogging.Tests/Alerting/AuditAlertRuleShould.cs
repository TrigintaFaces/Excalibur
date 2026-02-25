using Excalibur.Dispatch.AuditLogging.Alerting;
using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.AuditLogging.Tests.Alerting;

public class AuditAlertRuleShould
{
    [Fact]
    public void Create_rule_with_required_properties()
    {
        var rule = new AuditAlertRule
        {
            Name = "test-rule",
            Condition = _ => true
        };

        rule.Name.ShouldBe("test-rule");
        rule.Condition.ShouldNotBeNull();
    }

    [Fact]
    public void Default_severity_to_warning()
    {
        var rule = new AuditAlertRule
        {
            Name = "test-rule",
            Condition = _ => true
        };

        rule.Severity.ShouldBe(AuditAlertSeverity.Warning);
    }

    [Fact]
    public void Default_notification_channel_to_null()
    {
        var rule = new AuditAlertRule
        {
            Name = "test-rule",
            Condition = _ => true
        };

        rule.NotificationChannel.ShouldBeNull();
    }

    [Fact]
    public void Set_severity_and_notification_channel()
    {
        var rule = new AuditAlertRule
        {
            Name = "critical-rule",
            Condition = _ => true,
            Severity = AuditAlertSeverity.Critical,
            NotificationChannel = "pagerduty"
        };

        rule.Severity.ShouldBe(AuditAlertSeverity.Critical);
        rule.NotificationChannel.ShouldBe("pagerduty");
    }

    [Fact]
    public void Evaluate_condition_against_audit_event()
    {
        var rule = new AuditAlertRule
        {
            Name = "failed-auth-rule",
            Condition = e => e.EventType == AuditEventType.Authentication
                             && e.Outcome == AuditOutcome.Failure
        };

        var failedAuth = new AuditEvent
        {
            EventId = "evt-1",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Failure,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

        var successAuth = new AuditEvent
        {
            EventId = "evt-2",
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

        rule.Condition(failedAuth).ShouldBeTrue();
        rule.Condition(successAuth).ShouldBeFalse();
    }
}
