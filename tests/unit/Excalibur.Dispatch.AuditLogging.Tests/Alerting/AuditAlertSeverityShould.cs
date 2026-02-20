using Excalibur.Dispatch.AuditLogging.Alerting;

namespace Excalibur.Dispatch.AuditLogging.Tests.Alerting;

public class AuditAlertSeverityShould
{
    [Fact]
    public void Define_info_as_zero()
    {
        ((int)AuditAlertSeverity.Info).ShouldBe(0);
    }

    [Fact]
    public void Define_warning_as_one()
    {
        ((int)AuditAlertSeverity.Warning).ShouldBe(1);
    }

    [Fact]
    public void Define_critical_as_two()
    {
        ((int)AuditAlertSeverity.Critical).ShouldBe(2);
    }

    [Fact]
    public void Have_three_defined_values()
    {
        Enum.GetValues<AuditAlertSeverity>().Length.ShouldBe(3);
    }
}
