using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Options;

public class ProviderOptionsShould
{
    [Fact]
    public void Should_Default_Region_To_Empty()
    {
        var options = new ProviderOptions();

        options.Region.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_ConnectionString_To_Empty()
    {
        var options = new ProviderOptions();

        options.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_TimeoutMs_To_30000()
    {
        var options = new ProviderOptions();

        options.DefaultTimeoutMs.ShouldBe(30000);
    }

    [Fact]
    public void Should_Default_EnableDetailedLogging_To_False()
    {
        var options = new ProviderOptions();

        options.EnableDetailedLogging.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_RetryPolicy_To_New_Instance()
    {
        var options = new ProviderOptions();

        options.RetryPolicy.ShouldNotBeNull();
    }

    [Fact]
    public void Should_Default_Metadata_To_Empty_Dictionary()
    {
        var options = new ProviderOptions();

        options.Metadata.ShouldNotBeNull();
        options.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Azure,
            Region = "eastus",
            ConnectionString = "Endpoint=sb://...",
            DefaultTimeoutMs = 60000,
            EnableDetailedLogging = true,
        };

        options.Provider.ShouldBe(CloudProviderType.Azure);
        options.Region.ShouldBe("eastus");
        options.ConnectionString.ShouldBe("Endpoint=sb://...");
        options.DefaultTimeoutMs.ShouldBe(60000);
        options.EnableDetailedLogging.ShouldBeTrue();
    }
}
