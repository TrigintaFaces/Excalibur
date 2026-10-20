// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.Runtime;
using Amazon.SQS;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class AwsProviderMetadataConsumerShould
{
    [Fact]
    public void DeliverConfiguredExtensionMetadataThroughTheActualProviderRegistration()
    {
        var services = new ServiceCollection();
        services.AddAwsMessageBus(options =>
        {
            options.EnableSqs = true;
            options.EnableSns = false;
            options.EnableEventBridge = false;
            options.ServiceUrl = new Uri("http://localhost:4566");
            options.Region = "us-east-1";
        });
        services.Configure<AwsProviderOptions>(options =>
        {
            options.Connection.Credentials = new BasicAWSCredentials("local-key", "local-secret");
            options.Metadata["application-extension"] = "orders";
        });
        services.AddTransient<ProviderExtensionConsumer>();
        using var provider = services.BuildServiceProvider();

        var consumer = provider.GetRequiredService<ProviderExtensionConsumer>();
        consumer.Extension.ShouldBe("orders");
        consumer.Options.ShouldBeSameAs(provider.GetRequiredService<IOptions<AwsProviderOptions>>().Value);

        // Construct the real configured client without making a broker request.
        var client = provider.GetRequiredService<IAmazonSQS>();
        client.Config.ServiceURL.ShouldBe("http://localhost:4566/");
    }

    [Fact]
    public void PreserveConsumerSuppliedMetadataCollectionInitialization()
    {
        var entries = new Dictionary<string, string> { ["application-extension"] = "orders" };
        var options = new AwsProviderOptions { Metadata = entries };

        options.Metadata.ShouldBeSameAs(entries);
    }

    private sealed class ProviderExtensionConsumer(IOptions<AwsProviderOptions> options)
    {
        public AwsProviderOptions Options { get; } = options.Value;

        public string Extension => Options.Metadata["application-extension"];
    }
}
