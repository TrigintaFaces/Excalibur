namespace Excalibur.Dispatch.Compliance.Tests.Consent;

public class ConsentOptionsShould
{
    [Fact]
    public void Have_365_day_default_expiration()
    {
        var options = new ConsentOptions();

        options.DefaultExpirationDays.ShouldBe(365);
    }

    [Fact]
    public void Have_require_explicit_consent_enabled_by_default()
    {
        var options = new ConsentOptions();

        options.RequireExplicitConsent.ShouldBeTrue();
    }
}
