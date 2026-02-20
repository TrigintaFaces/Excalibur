using Excalibur.Dispatch.AuditLogging.Alerting;

namespace Excalibur.Dispatch.AuditLogging.Tests.Alerting;

public class EvaluationModeShould
{
    [Fact]
    public void Define_real_time_as_zero()
    {
        ((int)EvaluationMode.RealTime).ShouldBe(0);
    }

    [Fact]
    public void Define_batch_as_one()
    {
        ((int)EvaluationMode.Batch).ShouldBe(1);
    }

    [Fact]
    public void Have_two_defined_values()
    {
        Enum.GetValues<EvaluationMode>().Length.ShouldBe(2);
    }
}
