// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Wiring;

/// <summary>
/// Locks the connection options onto the AWS SDK client configuration. A configured endpoint that
/// does not reach the client is worse than an ignored knob: the host reads as though it targets an
/// emulator or a private endpoint while every call goes to the real AWS service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsConnectionOptionWiringShould
{
	[Fact]
	public void PointTheClientAtTheLocalStackEndpointRatherThanTheRegion()
	{
		var options = new AwsProviderOptions { Region = "us-east-1" };
		options.Connection.UseLocalStack = true;

		var config = new AmazonSQSConfig();
		AwsClientConfiguration.Apply(config, options);

		// The endpoint — not the region — decides where the call lands. Both arms matter: a config
		// that kept RegionEndpoint would resolve the real regional endpoint at call time.
		config.ServiceURL.ShouldContain("localhost:4566");
		config.RegionEndpoint.ShouldBeNull();
	}

	[Fact]
	public void PreferAnExplicitServiceUrlOverTheLocalStackDefault()
	{
		var options = new AwsProviderOptions { Region = "us-east-1" };
		options.Connection.UseLocalStack = true;
		options.Connection.ServiceUrl = new Uri("http://sqs.internal:9324");

		var config = new AmazonSQSConfig();
		AwsClientConfiguration.Apply(config, options);

		config.ServiceURL.ShouldContain("sqs.internal:9324");
	}

	[Fact]
	public void FallBackToTheRegionalEndpointWhenNoEndpointIsConfigured()
	{
		var options = new AwsProviderOptions { Region = "eu-west-2" };

		var config = new AmazonSQSConfig();
		AwsClientConfiguration.Apply(config, options);

		config.RegionEndpoint.ShouldNotBeNull();
		config.RegionEndpoint.SystemName.ShouldBe("eu-west-2");
	}

	[Fact]
	public void CarryTheRequestTimeoutAndRetryCountOntoTheClientConfig()
	{
		var options = new AwsProviderOptions
		{
			Region = "us-east-1",
			RequestTimeout = TimeSpan.FromSeconds(17),
			MaxRetryAttempts = 9,
		};

		var config = new AmazonSQSConfig();
		AwsClientConfiguration.Apply(config, options);

		config.Timeout.ShouldBe(TimeSpan.FromSeconds(17));
		config.MaxErrorRetry.ShouldBe(9);
	}

	[Fact]
	public void ResolveTheSqsClientAgainstTheConfiguredEndpoint()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsMessageBus(o =>
		{
			o.Region = "us-east-1";
			o.ServiceUrl = new Uri("http://localhost:4566");
			o.EnableSns = false;
			o.EnableEventBridge = false;
		});

		// Static credentials so the client can be constructed without an ambient credential chain;
		// the assertion is about the endpoint the client was built with.
		_ = services.PostConfigure<AwsProviderOptions>(
			o => o.Connection.Credentials = new BasicAWSCredentials("key", "secret"));

		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredService<IAmazonSQS>();

		client.Config.ServiceURL.ShouldContain("localhost:4566");
	}

	[Fact]
	public void ResolveTheSnsClientAgainstTheConfiguredEndpointAndTimeout()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSnsTransport("notifications", sns => sns
			.TopicArn("arn:aws:sns:us-east-1:123456789:topic")
			.Region("us-east-1")
			.ConfigureOptions(o =>
			{
				o.Connection.ServiceUrl = new Uri("http://localhost:4566");
				o.Connection.Timeout = TimeSpan.FromSeconds(11);
				o.Connection.UseHttp = true;
				o.Connection.AccessKey = "key";
				o.Connection.SecretKey = "secret";
			}));

		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredService<IAmazonSimpleNotificationService>();

		client.Config.ServiceURL.ShouldContain("localhost:4566");
		client.Config.Timeout.ShouldBe(TimeSpan.FromSeconds(11));
		client.Config.UseHttp.ShouldBeTrue();
	}
}
