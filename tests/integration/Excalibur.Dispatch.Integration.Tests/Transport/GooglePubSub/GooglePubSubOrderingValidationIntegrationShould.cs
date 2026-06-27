// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Testcontainers.PubSub;
using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Transport.GooglePubSub;

/// <summary>
/// NON-SKIPPED real-infra regression lock for bead <c>abyfxr</c> (sprint 855, FR-A3 / NFR-3): the Pub/Sub
/// startup validator MUST <b>fail loud</b> when an advertised delivery guarantee is configured
/// (<c>EnableMessageOrdering</c>) but the deployed subscription does NOT actually honor it — converting a
/// silently-inert flag into a clear startup error (the advertised-but-inert defect this sprint kills).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>). Backend's abyfxr wires
/// <c>PubSubSubscriptionConfigValidator</c> (an <see cref="IHostedService"/>) that does a read-only
/// <c>GetSubscriptionAsync</c> at startup and throws if the subscription lacks the configured guarantee.
/// This lock surfaced a 4th grounded gap (Tests 17057): the validator originally built its client with
/// <c>EmulatorDetection.None</c> (ignoring <c>PUBSUB_EMULATOR_HOST</c>) — fixed to
/// <c>EmulatorDetection.EmulatorOrProduction</c> so it's real-infra-verifiable here and correct for
/// emulator-dev consumers; production (no env var) is unchanged.
/// </para>
/// <para>
/// <b>Real-infra, NON-SKIPPED</b> (NFR-1): runs against the real Pub/Sub emulator. Asserts the
/// <i>observed</i> validator behavior (throw on a non-ordered subscription; pass on an ordered one), not
/// that an option was set. The validator is resolved by type name to isolate it from other hosted
/// services (no <c>InternalsVisibleTo</c>).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the throw case fails (no exception) if the validator does not actually read +
/// enforce the subscription's ordering property against the broker.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "GooglePubSub")]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Collection(GooglePubSubTransportCollection.Name)]
public sealed class GooglePubSubOrderingValidationIntegrationShould : IAsyncLifetime
{
	private const string ProjectId = "test-project";

	private PubSubContainer? _container;
	private PublisherServiceApiClient? _publisherApi;
	private SubscriberServiceApiClient? _subscriberApi;
	private bool _dockerAvailable;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new PubSubBuilder().Build();
			await TestTimeouts.WithTimeout(
				_container.StartAsync(),
				TestTimeouts.ContainerStart,
				"PubSub emulator container start").ConfigureAwait(false);

			var endpoint = _container.GetEmulatorEndpoint();
			Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", endpoint);

			_publisherApi = await new PublisherServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }
				.BuildAsync().ConfigureAwait(false);
			_subscriberApi = await new SubscriberServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }
				.BuildAsync().ConfigureAwait(false);

			// Probe the emulator actually answers gRPC (it may start but not speak HTTP/2 to the client).
			var probe = TopicName.FromProjectTopic(ProjectId, $"probe-{Guid.NewGuid():N}");
			await TestTimeouts.WithTimeout(_publisherApi.CreateTopicAsync(probe), TestTimeouts.HealthCheck, "probe create").ConfigureAwait(false);
			await TestTimeouts.WithTimeout(_publisherApi.DeleteTopicAsync(probe), TestTimeouts.HealthCheck, "probe delete").ConfigureAwait(false);

			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"PubSub emulator initialization failed: {ex.Message}");
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", null);
		if (_container is not null)
		{
			try
			{
				await _container.DisposeAsync().ConfigureAwait(false);
			}
			catch
			{
				// best-effort cleanup
			}
		}
	}

	[Fact]
	public async Task ThrowWhenOrderingConfiguredButSubscriptionDoesNotHonorIt()
	{
		_dockerAvailable.ShouldBeTrue("PubSub emulator must be available — real-infra fail-loud-validate proof (NFR-1)");

		// Arrange — a subscription WITHOUT message ordering.
		var subscriptionId = await CreateSubscriptionAsync(enableOrdering: false).ConfigureAwait(false);
		await using var provider = BuildProvider(subscriptionId, enableMessageOrdering: true);
		var validator = ResolveValidator(provider);

		// Act + Assert — configured EnableMessageOrdering=true vs a non-ordered sub MUST fail loud at startup.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => validator.StartAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task PassWhenOrderingConfiguredAndSubscriptionHonorsIt()
	{
		_dockerAvailable.ShouldBeTrue("PubSub emulator must be available — real-infra fail-loud-validate proof (NFR-1)");

		// Arrange — a subscription WITH message ordering.
		var subscriptionId = await CreateSubscriptionAsync(enableOrdering: true).ConfigureAwait(false);
		await using var provider = BuildProvider(subscriptionId, enableMessageOrdering: true);
		var validator = ResolveValidator(provider);

		// Act + Assert — the honored guarantee validates cleanly (no throw).
		await Should.NotThrowAsync(
			() => validator.StartAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	private async Task<string> CreateSubscriptionAsync(bool enableOrdering)
	{
		var topicId = $"abyfxr-topic-{Guid.NewGuid():N}";
		var subscriptionId = $"abyfxr-sub-{Guid.NewGuid():N}";
		var topicName = TopicName.FromProjectTopic(ProjectId, topicId);

		await _publisherApi!.CreateTopicAsync(topicName).ConfigureAwait(false);
		await _subscriberApi!.CreateSubscriptionAsync(new Subscription
		{
			SubscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId),
			TopicAsTopicName = topicName,
			AckDeadlineSeconds = 10,
			EnableMessageOrdering = enableOrdering,
		}).ConfigureAwait(false);

		return subscriptionId;
	}

	// Builds the abyfxr-wired transport (public API) with ordering configured via the public ConfigureOptions.
	private static ServiceProvider BuildProvider(string subscriptionId, bool enableMessageOrdering)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddGooglePubSubTransport("abyfxr", b => b
			.ProjectId(ProjectId)
			.TopicId("abyfxr-topic") // required at registration (eager TopicName build); validator reads the subscription
			.SubscriptionId(subscriptionId)
			.ConfigureOptions(o => o.EnableMessageOrdering = enableMessageOrdering));

		return services.BuildServiceProvider();
	}

	// Resolves the registered PubSubSubscriptionConfigValidator hosted service (isolated by type name — no
	// InternalsVisibleTo). Its presence when ordering is configured is the reachability guarantee.
	private static IHostedService ResolveValidator(IServiceProvider provider)
	{
		var validator = provider.GetServices<IHostedService>()
			.FirstOrDefault(h => string.Equals(
				h.GetType().Name, "PubSubSubscriptionConfigValidator", StringComparison.Ordinal));

		validator.ShouldNotBeNull(
			"abyfxr must register PubSubSubscriptionConfigValidator as an IHostedService when ordering is configured (reachability)");
		return validator;
	}
}
