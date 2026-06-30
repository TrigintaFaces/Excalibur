// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Applies optional, opt-in AWS provisioning for the SQS transport: the dead-letter redrive policy on
/// the source queue and the configured SNS-to-SQS subscriptions with their filter policies.
/// </summary>
/// <remarks>
/// This type performs network calls only when provisioning is explicitly enabled. It never creates a
/// queue topology implicitly; it applies the configuration the operator has already declared.
/// </remarks>
internal sealed partial class AwsSqsProvisioner
{
	private readonly IAmazonSQS _sqsClient;
	private readonly IAmazonSimpleNotificationService? _snsClient;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsProvisioner"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="snsClient">The AWS SNS client, or <see langword="null"/> when SNS is not registered.</param>
	/// <param name="logger">The logger instance.</param>
	public AwsSqsProvisioner(
		IAmazonSQS sqsClient,
		IAmazonSimpleNotificationService? snsClient,
		ILogger<AwsSqsProvisioner> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
		_snsClient = snsClient;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Applies the configured provisioning for the supplied transport options.
	/// </summary>
	/// <param name="options">The transport adapter options describing the desired topology.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task that completes when provisioning finishes.</returns>
	public async Task ProvisionAsync(AwsSqsTransportAdapterOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		var provisioning = options.Provisioning;
		if (!provisioning.Enabled)
		{
			return;
		}

		if (provisioning.ApplyDeadLetterRedrivePolicy)
		{
			await ApplyRedrivePolicyAsync(options, cancellationToken).ConfigureAwait(false);
		}

		if (provisioning.CreateSnsSubscriptions && options.HasSnsOptions)
		{
			await CreateSnsSubscriptionsAsync(options.SnsOptions!, cancellationToken).ConfigureAwait(false);
		}

		LogProvisioningCompleted(options.Name ?? string.Empty);
	}

	/// <summary>
	/// Sets the dead-letter redrive policy on the source queue so SQS natively moves messages to the
	/// configured dead-letter queue after the maximum receive count.
	/// </summary>
	private async Task ApplyRedrivePolicyAsync(
		AwsSqsTransportAdapterOptions options,
		CancellationToken cancellationToken)
	{
		var dlq = options.QueueOptions?.DeadLetterQueue;
		if (dlq is null || string.IsNullOrWhiteSpace(dlq.QueueArn))
		{
			return;
		}

		var sourceQueueUrl = options.HasQueueMappings
			? options.QueueMappings.Values.First()
			: options.Name;

		if (string.IsNullOrWhiteSpace(sourceQueueUrl) ||
			!Uri.TryCreate(sourceQueueUrl, UriKind.Absolute, out _))
		{
			LogProvisioningSkipped("redrive policy", "source queue URL is not an absolute SQS queue URL");
			return;
		}

		// RedrivePolicy is a JSON document; maxReceiveCount is a string per the SQS contract. The ARN
		// and integer are not user free-text in a way that requires escaping, so build it directly.
		var redrivePolicy = string.Format(
			CultureInfo.InvariantCulture,
			"{{\"deadLetterTargetArn\":\"{0}\",\"maxReceiveCount\":\"{1}\"}}",
			dlq.QueueArn,
			dlq.MaxReceiveCount);

		LogRedrivePolicyApplying(sourceQueueUrl, dlq.QueueArn);

		var request = new SetQueueAttributesRequest
		{
			QueueUrl = sourceQueueUrl,
			Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["RedrivePolicy"] = redrivePolicy,
			},
		};

		try
		{
			_ = await _sqsClient.SetQueueAttributesAsync(request, cancellationToken).ConfigureAwait(false);
			LogRedrivePolicyApplied(sourceQueueUrl);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogRedrivePolicyFailed(sourceQueueUrl, ex);
		}
	}

	/// <summary>
	/// Creates the configured SNS-to-SQS subscriptions and applies raw-message-delivery and filter
	/// policy attributes.
	/// </summary>
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Filter-policy JSON is only serialized when the operator opts into provisioning AND configures a filter policy; AOT/trimmed consumers leave provisioning disabled.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Filter-policy JSON is only serialized when the operator opts into provisioning AND configures a filter policy; AOT/trimmed consumers leave provisioning disabled.")]
	private async Task CreateSnsSubscriptionsAsync(
		AwsSqsSnsOptions snsOptions,
		CancellationToken cancellationToken)
	{
		if (_snsClient is null)
		{
			LogProvisioningSkipped("SNS subscriptions", "no SNS client is registered");
			return;
		}

		foreach (var subscription in snsOptions.Subscriptions)
		{
			if (string.IsNullOrWhiteSpace(subscription.TopicArn) ||
				string.IsNullOrWhiteSpace(subscription.QueueUrl))
			{
				LogProvisioningSkipped("SNS subscription", "topic ARN or queue URL is missing");
				continue;
			}

			try
			{
				var queueArn = await ResolveQueueArnAsync(subscription.QueueUrl, cancellationToken)
					.ConfigureAwait(false);
				if (string.IsNullOrWhiteSpace(queueArn))
				{
					LogProvisioningSkipped("SNS subscription", "could not resolve the queue ARN");
					continue;
				}

				LogSubscriptionCreating(subscription.TopicArn, queueArn);

				var subscribeResponse = await _snsClient.SubscribeAsync(
					new SubscribeRequest
					{
						TopicArn = subscription.TopicArn,
						Protocol = "sqs",
						Endpoint = queueArn,
						ReturnSubscriptionArn = true,
					},
					cancellationToken).ConfigureAwait(false);

				var subscriptionArn = subscribeResponse.SubscriptionArn;
				if (string.IsNullOrWhiteSpace(subscriptionArn))
				{
					LogProvisioningSkipped("SNS subscription attributes", "no subscription ARN was returned");
					continue;
				}

				var rawDelivery = subscription.RawMessageDelivery ?? snsOptions.RawMessageDelivery;
				if (rawDelivery)
				{
					await SetSubscriptionAttributeAsync(
						subscriptionArn, "RawMessageDelivery", "true", cancellationToken).ConfigureAwait(false);
				}

				if (subscription.FilterPolicy is { HasConditions: true } filterPolicy)
				{
					await SetSubscriptionAttributeAsync(
						subscriptionArn, "FilterPolicy", filterPolicy.ToJson(), cancellationToken)
						.ConfigureAwait(false);

					if (filterPolicy.Scope == AwsSqsFilterPolicyScope.MessageBody)
					{
						await SetSubscriptionAttributeAsync(
							subscriptionArn, "FilterPolicyScope", "MessageBody", cancellationToken)
							.ConfigureAwait(false);
					}
				}

				LogSubscriptionCreated(subscription.TopicArn, queueArn);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogSubscriptionFailed(subscription.TopicArn, ex);
			}
		}
	}

	private async Task SetSubscriptionAttributeAsync(
		string subscriptionArn,
		string name,
		string value,
		CancellationToken cancellationToken)
	{
		_ = await _snsClient!.SetSubscriptionAttributesAsync(
			new SetSubscriptionAttributesRequest
			{
				SubscriptionArn = subscriptionArn,
				AttributeName = name,
				AttributeValue = value,
			},
			cancellationToken).ConfigureAwait(false);
	}

	private async Task<string?> ResolveQueueArnAsync(string queueUrlOrArn, CancellationToken cancellationToken)
	{
		if (queueUrlOrArn.StartsWith("arn:", StringComparison.Ordinal))
		{
			return queueUrlOrArn;
		}

		var response = await _sqsClient.GetQueueAttributesAsync(
			new GetQueueAttributesRequest
			{
				QueueUrl = queueUrlOrArn,
				AttributeNames = ["QueueArn"],
			},
			cancellationToken).ConfigureAwait(false);

		return response.QueueARN;
	}

	[LoggerMessage(AwsSqsEventId.ProvisioningRedrivePolicyApplying, LogLevel.Information,
		"AWS SQS provisioning: applying redrive policy to {QueueUrl} -> {DeadLetterArn}")]
	private partial void LogRedrivePolicyApplying(string queueUrl, string deadLetterArn);

	[LoggerMessage(AwsSqsEventId.ProvisioningRedrivePolicyApplied, LogLevel.Information,
		"AWS SQS provisioning: redrive policy applied to {QueueUrl}")]
	private partial void LogRedrivePolicyApplied(string queueUrl);

	[LoggerMessage(AwsSqsEventId.ProvisioningRedrivePolicyFailed, LogLevel.Warning,
		"AWS SQS provisioning: failed to apply redrive policy to {QueueUrl}")]
	private partial void LogRedrivePolicyFailed(string queueUrl, Exception exception);

	[LoggerMessage(AwsSqsEventId.ProvisioningSubscriptionCreating, LogLevel.Information,
		"AWS SQS provisioning: subscribing queue {QueueArn} to topic {TopicArn}")]
	private partial void LogSubscriptionCreating(string topicArn, string queueArn);

	[LoggerMessage(AwsSqsEventId.ProvisioningSubscriptionCreated, LogLevel.Information,
		"AWS SQS provisioning: subscribed queue {QueueArn} to topic {TopicArn}")]
	private partial void LogSubscriptionCreated(string topicArn, string queueArn);

	[LoggerMessage(AwsSqsEventId.ProvisioningSubscriptionFailed, LogLevel.Warning,
		"AWS SQS provisioning: failed to subscribe to topic {TopicArn}")]
	private partial void LogSubscriptionFailed(string topicArn, Exception exception);

	[LoggerMessage(AwsSqsEventId.ProvisioningCompleted, LogLevel.Information,
		"AWS SQS provisioning: completed for transport {TransportName}")]
	private partial void LogProvisioningCompleted(string transportName);

	[LoggerMessage(AwsSqsEventId.ProvisioningSkipped, LogLevel.Information,
		"AWS SQS provisioning: skipped {Step} ({Reason})")]
	private partial void LogProvisioningSkipped(string step, string reason);
}
