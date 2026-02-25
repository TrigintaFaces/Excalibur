using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class CloudProviderTypeShould
{
    [Theory]
    [InlineData(CloudProviderType.Aws, 0)]
    [InlineData(CloudProviderType.Azure, 1)]
    [InlineData(CloudProviderType.Google, 2)]
    [InlineData(CloudProviderType.Kafka, 3)]
    [InlineData(CloudProviderType.RabbitMQ, 4)]
    [InlineData(CloudProviderType.Grpc, 5)]
    [InlineData(CloudProviderType.Custom, 6)]
    public void Should_Have_Correct_Values(CloudProviderType provider, int expected)
    {
        ((int)provider).ShouldBe(expected);
    }

    [Fact]
    public void Should_Have_Seven_Values()
    {
        Enum.GetValues<CloudProviderType>().Length.ShouldBe(7);
    }
}
