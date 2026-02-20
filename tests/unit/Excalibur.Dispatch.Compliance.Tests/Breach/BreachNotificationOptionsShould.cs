namespace Excalibur.Dispatch.Compliance.Tests.Breach;

public class BreachNotificationOptionsShould
{
    [Fact]
    public void Have_72_hour_notification_deadline_by_default()
    {
        var options = new BreachNotificationOptions();

        options.NotificationDeadlineHours.ShouldBe(72);
    }

    [Fact]
    public void Have_auto_notify_disabled_by_default()
    {
        var options = new BreachNotificationOptions();

        options.AutoNotify.ShouldBeFalse();
    }
}
