// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ProviderOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new ProviderOptions();

        options.Provider.ShouldBe(default);
        options.Region.ShouldBe(string.Empty);
        options.ConnectionString.ShouldBe(string.Empty);
        options.DefaultTimeoutMs.ShouldBe(30000);
        options.EnableDetailedLogging.ShouldBeFalse();
        options.RetryPolicy.ShouldNotBeNull();
        options.Metadata.ShouldNotBeNull();
        options.Metadata.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(CloudProviderType.Azure)]
    [InlineData(CloudProviderType.Aws)]
    [InlineData(CloudProviderType.Google)]
    [InlineData(CloudProviderType.RabbitMQ)]
    [InlineData(CloudProviderType.Kafka)]
    public void AllowSettingProvider(CloudProviderType provider)
    {
        var options = new ProviderOptions { Provider = provider };

        options.Provider.ShouldBe(provider);
    }

    [Theory]
    [InlineData("us-east-1")]
    [InlineData("westeurope")]
    [InlineData("asia-pacific-southeast1")]
    [InlineData("")]
    public void AllowSettingRegion(string region)
    {
        var options = new ProviderOptions { Region = region };

        options.Region.ShouldBe(region);
    }

    [Theory]
    [InlineData("Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey")]
    [InlineData("amqps://user:pass@rabbitmq.example.com:5671/vhost")]
    [InlineData("")]
    public void AllowSettingConnectionString(string connectionString)
    {
        var options = new ProviderOptions { ConnectionString = connectionString };

        options.ConnectionString.ShouldBe(connectionString);
    }

    [Theory]
    [InlineData(5000)]
    [InlineData(30000)]
    [InlineData(60000)]
    [InlineData(120000)]
    public void AllowSettingDefaultTimeoutMs(int timeout)
    {
        var options = new ProviderOptions { DefaultTimeoutMs = timeout };

        options.DefaultTimeoutMs.ShouldBe(timeout);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingEnableDetailedLogging(bool enable)
    {
        var options = new ProviderOptions { EnableDetailedLogging = enable };

        options.EnableDetailedLogging.ShouldBe(enable);
    }

    [Fact]
    public void AllowSettingRetryPolicy()
    {
        var retryPolicy = new RetryPolicyOptions
        {
            MaxRetryAttempts = 5,
            BaseDelayMs = 500,
            MaxDelayMs = 30000
        };

        var options = new ProviderOptions { RetryPolicy = retryPolicy };

        options.RetryPolicy.ShouldBeSameAs(retryPolicy);
    }

    [Fact]
    public void AllowAddingMetadata()
    {
        var options = new ProviderOptions
        {
            Metadata =
            {
                ["environment"] = "production",
                ["team"] = "platform"
            }
        };

        options.Metadata.Count.ShouldBe(2);
        options.Metadata["environment"].ShouldBe("production");
        options.Metadata["team"].ShouldBe("platform");
    }

    [Fact]
    public void AllowAzureServiceBusConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Azure,
            Region = "westeurope",
            ConnectionString = "Endpoint=sb://mybus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=xxx",
            DefaultTimeoutMs = 60000,
            EnableDetailedLogging = true,
            RetryPolicy = new RetryPolicyOptions
            {
                MaxRetryAttempts = 3,
                BaseDelayMs = 1000,
                MaxDelayMs = 30000
            }
        };

        options.Provider.ShouldBe(CloudProviderType.Azure);
        options.Region.ShouldBe("westeurope");
        options.ConnectionString.ShouldContain("servicebus.windows.net");
        options.DefaultTimeoutMs.ShouldBe(60000);
        options.EnableDetailedLogging.ShouldBeTrue();
        options.RetryPolicy.MaxRetryAttempts.ShouldBe(3);
    }

    [Fact]
    public void AllowAwsSqsConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Aws,
            Region = "us-east-1",
            DefaultTimeoutMs = 30000,
            EnableDetailedLogging = false,
            Metadata =
            {
                ["aws-access-key"] = "AKIAIOSFODNN7EXAMPLE"
            }
        };

        options.Provider.ShouldBe(CloudProviderType.Aws);
        options.Region.ShouldBe("us-east-1");
        options.Metadata["aws-access-key"].ShouldBe("AKIAIOSFODNN7EXAMPLE");
    }

    [Fact]
    public void AllowGooglePubSubConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Google,
            Region = "us-central1",
            DefaultTimeoutMs = 45000,
            Metadata =
            {
                ["project-id"] = "my-gcp-project"
            }
        };

        options.Provider.ShouldBe(CloudProviderType.Google);
        options.Region.ShouldBe("us-central1");
        options.Metadata["project-id"].ShouldBe("my-gcp-project");
    }

    [Fact]
    public void AllowRabbitMqConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.RabbitMQ,
            ConnectionString = "amqps://user:pass@rabbitmq.example.com:5671/vhost",
            DefaultTimeoutMs = 10000,
            EnableDetailedLogging = true
        };

        options.Provider.ShouldBe(CloudProviderType.RabbitMQ);
        options.ConnectionString.ShouldContain("amqps://");
        options.DefaultTimeoutMs.ShouldBe(10000);
    }

    [Fact]
    public void AllowKafkaConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Kafka,
            ConnectionString = "broker1:9092,broker2:9092,broker3:9092",
            DefaultTimeoutMs = 15000,
            Metadata =
            {
                ["security.protocol"] = "SSL",
                ["ssl.ca.location"] = "/var/private/ssl/ca-cert"
            }
        };

        options.Provider.ShouldBe(CloudProviderType.Kafka);
        options.ConnectionString.ShouldContain("broker");
        options.Metadata["security.protocol"].ShouldBe("SSL");
    }

    [Fact]
    public void AllowMinimalConfiguration()
    {
        var options = new ProviderOptions
        {
            Provider = CloudProviderType.Azure,
            ConnectionString = "connection-string"
        };

        options.Provider.ShouldBe(CloudProviderType.Azure);
        options.ConnectionString.ShouldBe("connection-string");
        options.Region.ShouldBe(string.Empty);
        options.DefaultTimeoutMs.ShouldBe(30000);
        options.EnableDetailedLogging.ShouldBeFalse();
    }

    [Fact]
    public void AllowRetryPolicyDefaults()
    {
        var options = new ProviderOptions();

        options.RetryPolicy.ShouldNotBeNull();
        // Verify RetryPolicy has its own defaults
        options.RetryPolicy.MaxRetryAttempts.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void AllowMetadataInitialization()
    {
        var options = new ProviderOptions
        {
            Metadata =
            {
                ["key1"] = "value1",
                ["key2"] = "value2",
                ["key3"] = "value3"
            }
        };

        options.Metadata.Count.ShouldBe(3);
        options.Metadata.ContainsKey("key1").ShouldBeTrue();
        options.Metadata.ContainsKey("key2").ShouldBeTrue();
        options.Metadata.ContainsKey("key3").ShouldBeTrue();
    }

    [Fact]
    public void AllowModifyingMetadataAfterCreation()
    {
        var options = new ProviderOptions();
        options.Metadata["new-key"] = "new-value";

        options.Metadata.Count.ShouldBe(1);
        options.Metadata["new-key"].ShouldBe("new-value");
    }
}
