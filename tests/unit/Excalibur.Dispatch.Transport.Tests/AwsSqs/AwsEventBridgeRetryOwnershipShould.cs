// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.EventBridge;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Binds single ownership of retry on the EventBridge send path.
/// </summary>
/// <remarks>
/// EventBridge only ever sends, so the dispatch pipeline owns retry outright and the SDK must own none of
/// it. The reason is specific to this transport: <c>PutEventsRequestEntry</c> carries no client token, so
/// an SDK retry emits a DISTINCT event rather than replaying one. With both budgets live, duplicates
/// multiply by their product, against a consumer whose only stated obligation is idempotency.
///
/// SQS and SNS already express this rule, and express it loudly — their send clients throw a
/// <c>ConfigurationException</c> rather than run with a non-zero budget. EventBridge had neither the guard
/// nor zero retries on either registration path, which is the gap these arms close. It is also the one
/// transport that raises <c>DispatchDeliveryUncertainException</c>, because its acknowledgement is
/// genuinely ambiguous — so it was the worst possible place to leave a second retrier running.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeRetryOwnershipShould
{
	/// <summary>
	/// The shared-provider registration path hands the client a zero error budget.
	/// </summary>
	/// <remarks>
	/// RED against pre-fix source. This path applied <c>AwsClientConfiguration.Apply</c>, which sets
	/// <c>MaxErrorRetry = AwsProviderOptions.MaxRetryAttempts</c> — and that property defaults to 3, so the
	/// defect bit with no configuration at all. The arm deliberately leaves the default in place rather
	/// than setting it to zero, because a test that configures the budget away cannot observe the bug.
	/// </remarks>
	[Fact]
	public void Own_retry_on_the_shared_provider_registration()
	{
		var services = new ServiceCollection();
		_ = services.Configure<AwsProviderOptions>(options =>
		{
			options.Connection.ServiceUrl = new Uri("http://eventbridge.localhost:4566");
		});
		_ = services.AddAwsMessageBus(options => options.EnableEventBridge = true);

		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredService<IAmazonEventBridge>();

		client.Config.MaxErrorRetry.ShouldBe(0, "the dispatch pipeline owns retry; the SDK must own none");
		client.Config.MaxStaleConnectionRetries.ShouldBe(0);
	}

	/// <summary>
	/// The named-transport registration path hands the client a zero error budget.
	/// </summary>
	/// <remarks>
	/// RED against pre-fix source for a different reason than the arm above, which is why both exist: this
	/// path constructed a bare <c>new AmazonEventBridgeClient()</c> with no config object at all, so it
	/// inherited the AWS SDK's own default budget rather than any value of ours.
	/// </remarks>
	[Fact]
	public void Own_retry_on_the_named_transport_registration()
	{
		var services = new ServiceCollection();
		_ = services.AddAwsEventBridgeTransport("orders", builder => _ = builder.Region("us-east-1"));

		using var provider = services.BuildServiceProvider();
		var client = provider.GetRequiredKeyedService<IAmazonEventBridge>("orders");

		client.Config.MaxErrorRetry.ShouldBe(0, "a named EventBridge transport is still a send-only client");
		client.Config.MaxStaleConnectionRetries.ShouldBe(0);
	}
}
