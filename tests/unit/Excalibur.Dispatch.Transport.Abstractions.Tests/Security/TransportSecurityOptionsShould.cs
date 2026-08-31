using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Security;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TransportSecurityOptionsShould
{
    [Fact]
    public void Should_Default_RequireTls_To_True()
    {
        var options = new TransportSecurityOptions();

        options.RequireTls.ShouldBeTrue();
    }

    [Fact]
    public void Should_Allow_Disabling_RequireTls()
    {
        var options = new TransportSecurityOptions { RequireTls = false };

        options.RequireTls.ShouldBeFalse();
    }
}
