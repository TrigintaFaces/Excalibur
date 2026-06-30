// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// Unit tests covering the AWS SQS client config honoring the configured region, retry count, and
/// timeout (bead vhup48), plus the visibility-heartbeat option defaults. Pre-fix the wired client was
/// constructed with a bare <c>new AmazonSQSClient()</c>, ignoring every configured option, and the
/// builder did not expose retry/timeout at all.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AwsSqsClientAndConsumerOptionsShould
{
	[Fact]
	public void BuildSqsConfig_HonoringConfiguredRegionRetryAndTimeout()
	{
		// Arrange — configure region, retry count, and request timeout via the public builder.
		var options = new AwsSqsTransportAdapterOptions { Name = "orders" };
		var builder = new AwsSqsTransportBuilder(options);
		_ = builder
			.UseRegion("us-west-2")
			.UseMaxRetryAttempts(7)
			.UseRequestTimeout(TimeSpan.FromSeconds(42));

		// Act — build the SQS client config from the configured options.
		var config = AwsSqsTransportServiceCollectionExtensions.CreateSqsConfig(options);

		// Assert — the config reflects the configured values, not the SDK defaults / a bare client.
		config.RegionEndpoint.ShouldNotBeNull();
		config.RegionEndpoint.SystemName.ShouldBe("us-west-2");
		config.MaxErrorRetry.ShouldBe(7);
		config.Timeout.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void DefaultVisibilityHeartbeat_IsDisabledWithSafeDefaults()
	{
		// The heartbeat must be opt-in so default consumer behavior is unchanged.
		var options = new AwsSqsVisibilityHeartbeatOptions();

		options.Enabled.ShouldBeFalse();
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		options.MaxExtension.ShouldBe(TimeSpan.FromMinutes(10));
	}
}
