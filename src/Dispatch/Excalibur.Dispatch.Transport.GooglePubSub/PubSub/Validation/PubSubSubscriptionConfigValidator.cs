// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Startup validator (abyfxr, FR-A3) that fails loud when an advertised Pub/Sub delivery guarantee is
/// configured but the target subscription does not actually provide it. When
/// <see cref="GooglePubSubTransportOptions.EnableMessageOrdering"/> and/or
/// <see cref="GooglePubSubTransportOptions.EnableExactlyOnceDelivery"/> is requested, this performs a
/// <b>read-only</b> <see cref="SubscriberServiceApiClient.GetSubscriptionAsync(SubscriptionName, global::Google.Api.Gax.Grpc.CallSettings)"/>
/// at startup and throws a clear configuration error if the existing subscription lacks the property —
/// converting a silently-inert flag into a loud, actionable failure (NFR-3).
/// </summary>
/// <remarks>
/// This validator NEVER creates or mutates the subscription (no <c>CreateSubscription</c>); provisioning
/// subscriptions with the required properties is an infrastructure concern (IaC/Terraform) or an opt-in
/// auto-create capability tracked separately. It only verifies, read-only, that the deployed
/// subscription honors the configured guarantees before consumption begins.
/// </remarks>
internal sealed partial class PubSubSubscriptionConfigValidator : IHostedService
{
	private readonly string _projectId;
	private readonly string _subscriptionId;
	private readonly bool _requireOrdering;
	private readonly bool _requireExactlyOnce;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubSubscriptionConfigValidator"/> class.
	/// </summary>
	/// <param name="projectId">The Google Cloud project id.</param>
	/// <param name="subscriptionId">The Pub/Sub subscription id to validate.</param>
	/// <param name="requireOrdering">Whether message ordering must be enabled on the subscription.</param>
	/// <param name="requireExactlyOnce">Whether exactly-once delivery must be enabled on the subscription.</param>
	/// <param name="logger">The logger instance.</param>
	public PubSubSubscriptionConfigValidator(
		string projectId,
		string subscriptionId,
		bool requireOrdering,
		bool requireExactlyOnce,
		ILogger<PubSubSubscriptionConfigValidator> logger)
	{
		_projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
		_subscriptionId = subscriptionId ?? throw new ArgumentNullException(nameof(subscriptionId));
		_requireOrdering = requireOrdering;
		_requireExactlyOnce = requireExactlyOnce;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_requireOrdering && !_requireExactlyOnce)
		{
			return;
		}

		var subscriptionName = new SubscriptionName(_projectId, _subscriptionId);

		// EmulatorOrProduction respects PUBSUB_EMULATOR_HOST when set (so the fail-loud validation is
		// real-infra-provable against the Pub/Sub emulator) and falls back to production credentials when
		// the env var is absent — production behavior is unchanged (abyfxr, Tests' grounded NFR-3 finding).
		var client = await new SubscriberServiceApiClientBuilder
		{
			EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);
		var subscription = await client.GetSubscriptionAsync(subscriptionName, cancellationToken).ConfigureAwait(false);

		if (_requireOrdering && !subscription.EnableMessageOrdering)
		{
			throw new InvalidOperationException(
				$"Google Pub/Sub subscription '{subscriptionName}' is configured with EnableMessageOrdering=true " +
				"but the deployed subscription does NOT have message ordering enabled. Per-ordering-key FIFO " +
				"delivery would be silently lost. Recreate the subscription with EnableMessageOrdering=true " +
				"(via IaC/console) so the advertised ordering guarantee is honored.");
		}

		if (_requireExactlyOnce && !subscription.EnableExactlyOnceDelivery)
		{
			throw new InvalidOperationException(
				$"Google Pub/Sub subscription '{subscriptionName}' is configured with EnableExactlyOnceDelivery=true " +
				"but the deployed subscription does NOT have exactly-once delivery enabled. The advertised " +
				"exactly-once guarantee would be silently lost. Recreate the subscription with " +
				"EnableExactlyOnceDelivery=true (via IaC/console) so the guarantee is honored.");
		}

		LogSubscriptionValidated(_subscriptionId, _requireOrdering, _requireExactlyOnce);
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(GooglePubSubEventId.SubscriptionConfigValidated, LogLevel.Information,
		"Google Pub/Sub subscription {SubscriptionId} delivery guarantees validated (ordering={RequireOrdering}, exactlyOnce={RequireExactlyOnce})")]
	private partial void LogSubscriptionValidated(string subscriptionId, bool requireOrdering, bool requireExactlyOnce);
}
