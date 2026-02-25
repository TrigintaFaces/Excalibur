using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Security;

public class TransportSecurityOptionsShould
{
    [Fact]
    public void Should_Default_RequireTls_To_True()
    {
        var options = new TransportSecurityOptions();

        options.RequireTls.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_ValidateServerCertificate_To_True()
    {
        var options = new TransportSecurityOptions();

        options.ValidateServerCertificate.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_MinimumTlsVersion_To_Tls12()
    {
        var options = new TransportSecurityOptions();

        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls12);
    }

    [Fact]
    public void Should_Default_ClientCertificatePath_To_Null()
    {
        var options = new TransportSecurityOptions();

        options.ClientCertificatePath.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_ClientCertificateKeyPath_To_Null()
    {
        var options = new TransportSecurityOptions();

        options.ClientCertificateKeyPath.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_ClientCertificatePassword_To_Null()
    {
        var options = new TransportSecurityOptions();

        options.ClientCertificatePassword.ShouldBeNull();
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var options = new TransportSecurityOptions
        {
            RequireTls = false,
            ValidateServerCertificate = false,
            MinimumTlsVersion = TlsVersion.Tls13,
            ClientCertificatePath = "/certs/client.pem",
            ClientCertificateKeyPath = "/certs/client.key",
            ClientCertificatePassword = "secret",
        };

        options.RequireTls.ShouldBeFalse();
        options.ValidateServerCertificate.ShouldBeFalse();
        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls13);
        options.ClientCertificatePath.ShouldBe("/certs/client.pem");
        options.ClientCertificateKeyPath.ShouldBe("/certs/client.key");
        options.ClientCertificatePassword.ShouldBe("secret");
    }
}
