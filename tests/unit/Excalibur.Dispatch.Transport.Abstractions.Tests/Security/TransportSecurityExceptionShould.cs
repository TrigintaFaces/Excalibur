using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Security;

public class TransportSecurityExceptionShould
{
    [Fact]
    public void Should_Create_With_Default_Message()
    {
        var ex = new TransportSecurityException();

        ex.Message.ShouldBe("Transport security requirements were not met.");
    }

    [Fact]
    public void Should_Create_With_Custom_Message()
    {
        var ex = new TransportSecurityException("TLS required");

        ex.Message.ShouldBe("TLS required");
    }

    [Fact]
    public void Should_Create_With_Inner_Exception()
    {
        var inner = new InvalidOperationException("SSL handshake failed");
        var ex = new TransportSecurityException("TLS failed", inner);

        ex.Message.ShouldBe("TLS failed");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Should_Set_TransportName()
    {
        var ex = new TransportSecurityException("error") { TransportName = "Kafka" };

        ex.TransportName.ShouldBe("Kafka");
    }

    [Fact]
    public void Should_Set_FailureReason()
    {
        var ex = new TransportSecurityException("error")
        {
            FailureReason = TransportSecurityFailureReason.TlsNotEnabled,
        };

        ex.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
    }

    [Fact]
    public void Should_Default_FailureReason_To_Unspecified()
    {
        var ex = new TransportSecurityException();

        ex.FailureReason.ShouldBe(TransportSecurityFailureReason.Unspecified);
    }

    [Fact]
    public void Should_Inherit_From_InvalidOperationException()
    {
        var ex = new TransportSecurityException("test");

        ex.ShouldBeAssignableTo<InvalidOperationException>();
    }
}
