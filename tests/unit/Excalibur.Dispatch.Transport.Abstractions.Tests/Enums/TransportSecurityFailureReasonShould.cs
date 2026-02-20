using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class TransportSecurityFailureReasonShould
{
    [Theory]
    [InlineData(TransportSecurityFailureReason.Unspecified, 0)]
    [InlineData(TransportSecurityFailureReason.TlsNotEnabled, 1)]
    [InlineData(TransportSecurityFailureReason.TlsVersionTooLow, 2)]
    [InlineData(TransportSecurityFailureReason.CertificateValidationFailed, 3)]
    [InlineData(TransportSecurityFailureReason.ClientCertificateFailed, 4)]
    [InlineData(TransportSecurityFailureReason.ConnectionDowngraded, 5)]
    public void Should_Have_Correct_Values(TransportSecurityFailureReason reason, int expected)
    {
        ((int)reason).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Six_Values()
    {
        Enum.GetValues<TransportSecurityFailureReason>().Length.ShouldBe(6);
    }
}
