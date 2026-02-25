namespace Excalibur.Dispatch.Compliance.Tests.Monitoring;

public class AlertSeverityShould
{
    [Theory]
    [InlineData(5, AlertSeverity.Critical)]
    [InlineData(10, AlertSeverity.Critical)]
    [InlineData(3, AlertSeverity.High)]
    [InlineData(4, AlertSeverity.High)]
    [InlineData(1, AlertSeverity.Medium)]
    [InlineData(2, AlertSeverity.Medium)]
    public void Map_rotation_failure_consecutive_failures_to_severity(int failures, AlertSeverity expected)
    {
        var alert = new KeyRotationFailureAlert("key", "provider", "error", DateTimeOffset.UtcNow, failures);

        alert.Severity.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, AlertSeverity.Critical)]
    [InlineData(1, AlertSeverity.Critical)]
    [InlineData(5, AlertSeverity.High)]
    [InlineData(7, AlertSeverity.High)]
    [InlineData(10, AlertSeverity.Medium)]
    [InlineData(14, AlertSeverity.Medium)]
    [InlineData(15, AlertSeverity.Low)]
    [InlineData(30, AlertSeverity.Low)]
    public void Map_expiration_days_to_severity(int daysUntilExpiration, AlertSeverity expected)
    {
        var alert = new KeyExpirationAlert(
            "key", "provider", DateTimeOffset.UtcNow.AddDays(daysUntilExpiration), daysUntilExpiration);

        alert.Severity.ShouldBe(expected);
    }
}
