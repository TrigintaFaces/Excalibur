using Excalibur.Dispatch.AuditLogging.Alerting;

namespace Excalibur.Dispatch.AuditLogging.Tests.Alerting;

public class AuditAlertOptionsShould
{
    [Fact]
    public void Default_evaluation_mode_to_real_time()
    {
        var options = new AuditAlertOptions();

        options.EvaluationMode.ShouldBe(EvaluationMode.RealTime);
    }

    [Fact]
    public void Default_max_alerts_per_minute_to_100()
    {
        var options = new AuditAlertOptions();

        options.MaxAlertsPerMinute.ShouldBe(100);
    }

    [Fact]
    public void Allow_setting_evaluation_mode()
    {
        var options = new AuditAlertOptions { EvaluationMode = EvaluationMode.Batch };

        options.EvaluationMode.ShouldBe(EvaluationMode.Batch);
    }

    [Fact]
    public void Allow_setting_max_alerts_per_minute()
    {
        var options = new AuditAlertOptions { MaxAlertsPerMinute = 50 };

        options.MaxAlertsPerMinute.ShouldBe(50);
    }
}
