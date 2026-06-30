// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Api.Gax;
using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Opt-in startup service that auto-applies the configured dead letter policy to the transport's
/// subscription, so a configured dead letter topic is actually honored rather than built but never
/// attached. When enabled, this performs a <c>GetSubscription</c> + <c>UpdateSubscription</c> at startup
/// (via <see cref="PubSubDeadLetterQueueManager.ConfigureDeadLetterPolicyAsync"/>) to set the
/// subscription's <see cref="DeadLetterPolicy"/>.
/// </summary>
/// <remarks>
/// This service mutates the subscription and is therefore <b>opt-in</b> (default off): in most
/// deployments subscription provisioning is owned by infrastructure-as-code, and the read-only
/// <see cref="PubSubSubscriptionConfigValidator"/> is the safe default. Enable this only when the
/// application is intended to own and reconcile its dead letter policy at startup.
/// </remarks>
internal sealed partial class PubSubDeadLetterPolicyApplier : IHostedService
{
	private readonly string _projectId;
	private readonly string _subscriptionId;
	private readonly string _deadLetterTopicId;
	private readonly int _maxDeliveryAttempts;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<PubSubDeadLetterPolicyApplier> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PubSubDeadLetterPolicyApplier"/> class.
	/// </summary>
	/// <param name="projectId">The Google Cloud project id.</param>
	/// <param name="subscriptionId">The Pub/Sub subscription id to attach the policy to.</param>
	/// <param name="deadLetterTopicId">The dead letter topic id to route poison messages to.</param>
	/// <param name="maxDeliveryAttempts">The maximum delivery attempts before dead-lettering.</param>
	/// <param name="loggerFactory">The logger factory used to create loggers for this service and the dead letter queue manager.</param>
	public PubSubDeadLetterPolicyApplier(
		string projectId,
		string subscriptionId,
		string deadLetterTopicId,
		int maxDeliveryAttempts,
		ILoggerFactory loggerFactory)
	{
		_projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
		_subscriptionId = subscriptionId ?? throw new ArgumentNullException(nameof(subscriptionId));
		_deadLetterTopicId = deadLetterTopicId ?? throw new ArgumentNullException(nameof(deadLetterTopicId));
		_maxDeliveryAttempts = maxDeliveryAttempts;
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_logger = loggerFactory.CreateLogger<PubSubDeadLetterPolicyApplier>();
	}

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var subscriptionName = new SubscriptionName(_projectId, _subscriptionId);
		var deadLetterTopic = new TopicName(_projectId, _deadLetterTopicId);

		// EmulatorOrProduction respects PUBSUB_EMULATOR_HOST when set (so the apply is real-infra-provable
		// against the Pub/Sub emulator) and falls back to production credentials when the env var is absent.
		var subscriberClient = await new SubscriberServiceApiClientBuilder
		{
			EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);

		var publisherClient = await new PublisherServiceApiClientBuilder
		{
			EmulatorDetection = EmulatorDetection.EmulatorOrProduction,
		}.BuildAsync(cancellationToken).ConfigureAwait(false);

		using var manager = new PubSubDeadLetterQueueManager(
			subscriberClient,
			publisherClient,
			Microsoft.Extensions.Options.Options.Create(new DeadLetterOptions { DeadLetterTopicName = deadLetterTopic }),
			_loggerFactory.CreateLogger<PubSubDeadLetterQueueManager>());

		try
		{
			_ = await manager.ConfigureDeadLetterPolicyAsync(
				subscriptionName,
				deadLetterTopic,
				_maxDeliveryAttempts,
				cancellationToken).ConfigureAwait(false);

			LogDeadLetterPolicyApplied(_subscriptionId, _deadLetterTopicId, _maxDeliveryAttempts);
		}
		catch (Exception ex)
		{
			LogDeadLetterPolicyApplyFailed(_subscriptionId, _deadLetterTopicId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(GooglePubSubEventId.DeadLetterPolicyAutoApplied, LogLevel.Information,
		"Auto-applied dead letter policy to Google Pub/Sub subscription {SubscriptionId}: topic={DeadLetterTopicId}, maxAttempts={MaxAttempts}")]
	private partial void LogDeadLetterPolicyApplied(string subscriptionId, string deadLetterTopicId, int maxAttempts);

	[LoggerMessage(GooglePubSubEventId.DeadLetterPolicyAutoApplyFailed, LogLevel.Error,
		"Failed to auto-apply dead letter policy to Google Pub/Sub subscription {SubscriptionId} (topic={DeadLetterTopicId})")]
	private partial void LogDeadLetterPolicyApplyFailed(string subscriptionId, string deadLetterTopicId, Exception ex);
}
