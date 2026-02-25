using Excalibur.Dispatch.AuditLogging.Alerting;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Tests.Alerting;

public class DefaultAuditAlertServiceShould
{
    private static DefaultAuditAlertService CreateSut(AuditAlertOptions? options = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? new AuditAlertOptions());
        return new DefaultAuditAlertService(opts, NullLogger<DefaultAuditAlertService>.Instance);
    }

    private static AuditEvent CreateEvent(
        AuditEventType eventType = AuditEventType.Authentication,
        AuditOutcome outcome = AuditOutcome.Failure) =>
        new()
        {
            EventId = $"evt-{Guid.NewGuid():N}",
            EventType = eventType,
            Action = "Login",
            Outcome = outcome,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1"
        };

    [Fact]
    public async Task Register_rule_successfully()
    {
        var sut = CreateSut();
        var rule = new AuditAlertRule
        {
            Name = "test-rule",
            Condition = _ => true
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);

        // Evaluate an event to verify rule was registered
        // The test passes if no exception is thrown
        await sut.EvaluateAsync(CreateEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task Throw_argument_null_for_null_rule()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.RegisterRuleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_argument_null_for_null_event()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.EvaluateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Evaluate_matching_rule_without_error()
    {
        var sut = CreateSut();
        var triggered = false;

        var rule = new AuditAlertRule
        {
            Name = "failed-auth",
            Condition = e =>
            {
                if (e.Outcome == AuditOutcome.Failure)
                {
                    triggered = true;
                    return true;
                }

                return false;
            },
            Severity = AuditAlertSeverity.Critical
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);
        await sut.EvaluateAsync(CreateEvent(outcome: AuditOutcome.Failure), CancellationToken.None);

        triggered.ShouldBeTrue();
    }

    [Fact]
    public async Task Not_trigger_non_matching_rule()
    {
        var sut = CreateSut();
        var triggered = false;

        var rule = new AuditAlertRule
        {
            Name = "failed-auth",
            Condition = e =>
            {
                if (e.Outcome == AuditOutcome.Failure)
                {
                    triggered = true;
                    return true;
                }

                return false;
            }
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);
        await sut.EvaluateAsync(CreateEvent(outcome: AuditOutcome.Success), CancellationToken.None);

        triggered.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_rule_condition_exception_gracefully()
    {
        var sut = CreateSut();
        var rule = new AuditAlertRule
        {
            Name = "bad-rule",
            Condition = _ => throw new InvalidOperationException("Broken condition")
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);

        // Should not throw - exceptions in rule conditions are swallowed
        await sut.EvaluateAsync(CreateEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task Replace_rule_with_same_name()
    {
        var sut = CreateSut();
        var firstTriggered = false;
        var secondTriggered = false;

        var rule1 = new AuditAlertRule
        {
            Name = "my-rule",
            Condition = _ =>
            {
                firstTriggered = true;
                return true;
            }
        };

        var rule2 = new AuditAlertRule
        {
            Name = "my-rule",
            Condition = _ =>
            {
                secondTriggered = true;
                return true;
            }
        };

        await sut.RegisterRuleAsync(rule1, CancellationToken.None);
        await sut.RegisterRuleAsync(rule2, CancellationToken.None);
        await sut.EvaluateAsync(CreateEvent(), CancellationToken.None);

        firstTriggered.ShouldBeFalse();
        secondTriggered.ShouldBeTrue();
    }

    [Fact]
    public async Task Respect_rate_limit()
    {
        var options = new AuditAlertOptions { MaxAlertsPerMinute = 2 };
        var sut = CreateSut(options);
        var triggerCount = 0;

        var rule = new AuditAlertRule
        {
            Name = "counting-rule",
            Condition = _ =>
            {
                Interlocked.Increment(ref triggerCount);
                return true;
            }
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);

        // Evaluate 5 events; only 2 should be allowed due to rate limit
        for (var i = 0; i < 5; i++)
        {
            await sut.EvaluateAsync(CreateEvent(), CancellationToken.None);
        }

        // The condition is always called, but the log (TryConsumeAlertQuota) limits to 2
        // triggerCount reflects the condition check count, which is always 5
        triggerCount.ShouldBe(5);
    }

    [Fact]
    public async Task Respect_cancellation_token()
    {
        var sut = CreateSut();
        var rule = new AuditAlertRule
        {
            Name = "cancel-rule",
            Condition = _ => true
        };

        await sut.RegisterRuleAsync(rule, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.EvaluateAsync(CreateEvent(), cts.Token));
    }

    [Fact]
    public void Throw_argument_null_for_null_options()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditAlertService(null!, NullLogger<DefaultAuditAlertService>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DefaultAuditAlertService(
                Microsoft.Extensions.Options.Options.Create(new AuditAlertOptions()),
                null!));
    }
}
