using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Options;

public class CloudMessagingOptionsShould
{
    [Fact]
    public void Should_Default_DefaultProvider_To_Null()
    {
        var options = new CloudMessagingOptions();

        options.DefaultProvider.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_Providers_To_Empty_OrdinalIgnoreCase_Dictionary()
    {
        var options = new CloudMessagingOptions();

        options.Providers.ShouldNotBeNull();
        options.Providers.Count.ShouldBe(0);
    }

    [Fact]
    public void Providers_Should_Use_OrdinalIgnoreCase_Comparer()
    {
        var options = new CloudMessagingOptions();
        options.Providers["azure"] = new ProviderOptions { Provider = CloudProviderType.Azure };

        options.Providers.TryGetValue("AZURE", out var provider).ShouldBeTrue();
        provider!.Provider.ShouldBe(CloudProviderType.Azure);
    }

    [Fact]
    public void Should_Default_EnableTracing_To_True()
    {
        var options = new CloudMessagingOptions();

        options.EnableTracing.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_EnableMetrics_To_True()
    {
        var options = new CloudMessagingOptions();

        options.EnableMetrics.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_GlobalTimeout_To_30_Seconds()
    {
        var options = new CloudMessagingOptions();

        options.GlobalTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
