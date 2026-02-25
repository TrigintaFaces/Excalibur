namespace Excalibur.Dispatch.Compliance.Tests.SubjectAccess;

public class SubjectAccessOptionsShould
{
    [Fact]
    public void Have_30_day_response_deadline_by_default()
    {
        var options = new SubjectAccessOptions();

        options.ResponseDeadlineDays.ShouldBe(30);
    }

    [Fact]
    public void Have_auto_fulfill_disabled_by_default()
    {
        var options = new SubjectAccessOptions();

        options.AutoFulfill.ShouldBeFalse();
    }
}
