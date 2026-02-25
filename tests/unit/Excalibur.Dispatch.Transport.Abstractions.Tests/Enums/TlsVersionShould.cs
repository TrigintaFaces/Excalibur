using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class TlsVersionShould
{
    [Theory]
    [InlineData(TlsVersion.Tls10, 0)]
    [InlineData(TlsVersion.Tls11, 1)]
    [InlineData(TlsVersion.Tls12, 2)]
    [InlineData(TlsVersion.Tls13, 3)]
    public void Should_Have_Correct_Values(TlsVersion version, int expected)
    {
        ((int)version).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Four_Values()
    {
        Enum.GetValues<TlsVersion>().Length.ShouldBe(4);
    }
}
