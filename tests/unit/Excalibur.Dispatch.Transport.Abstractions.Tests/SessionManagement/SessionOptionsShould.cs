using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionOptionsShould
{
    [Fact]
    public void Have_sensible_defaults()
    {
        // Arrange & Act
        var sut = new SessionOptions();

        // Assert
        sut.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
        sut.LockTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        sut.AutoRenew.ShouldBeTrue();
        sut.AutoRenewInterval.ShouldBe(TimeSpan.FromMinutes(1));
        sut.MaxMessagesPerSession.ShouldBeNull();
        sut.PreserveOrder.ShouldBeTrue();
        sut.Metadata.ShouldNotBeNull();
        sut.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange & Act
        var sut = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(30),
            LockTimeout = TimeSpan.FromSeconds(60),
            AutoRenew = false,
            AutoRenewInterval = TimeSpan.FromMinutes(5),
            MaxMessagesPerSession = 1000,
            PreserveOrder = false
        };
        sut.Metadata["env"] = "production";

        // Assert
        sut.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(30));
        sut.LockTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        sut.AutoRenew.ShouldBeFalse();
        sut.AutoRenewInterval.ShouldBe(TimeSpan.FromMinutes(5));
        sut.MaxMessagesPerSession.ShouldBe(1000);
        sut.PreserveOrder.ShouldBeFalse();
        sut.Metadata["env"].ShouldBe("production");
    }
}
