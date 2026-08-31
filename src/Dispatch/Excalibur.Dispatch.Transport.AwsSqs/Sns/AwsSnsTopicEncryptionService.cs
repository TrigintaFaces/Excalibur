// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Applies the requested server-side encryption key to the configured SNS topic during host start-up.
/// </summary>
/// <remarks>
/// <para>
/// Server-side encryption on SNS is a property of the topic, not of an individual publish call, so a
/// transport that only publishes cannot make an unencrypted topic encrypted. When encryption is
/// requested this service sets the topic's <c>KmsMasterKeyId</c> attribute so the request actually
/// reaches AWS instead of being held in configuration.
/// </para>
/// <para>
/// The service is inert unless encryption was requested. When it has been requested it is
/// <b>fail-closed</b>: a missing key, or a failure to apply the key, aborts start-up rather than
/// letting the host run while publishing to a topic the operator believes is encrypted. This differs
/// deliberately from the fail-open behaviour of the queue provisioning path, where a failure costs
/// throughput rather than confidentiality.
/// </para>
/// </remarks>
internal sealed partial class AwsSnsTopicEncryptionService : IHostedService
{
	private const string KmsMasterKeyIdAttribute = "KmsMasterKeyId";

	private readonly IAmazonSimpleNotificationService _snsClient;
	private readonly IOptionsMonitor<AwsSnsOptions> _options;
	private readonly string _transportName;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSnsTopicEncryptionService"/> class.
	/// </summary>
	/// <param name="snsClient">The AWS SNS client.</param>
	/// <param name="options">The SNS transport options monitor.</param>
	/// <param name="transportName">
	/// The name of the transport registration whose options this applier acts on. The options are
	/// registered per name, so an applier that read the unnamed instance would act on whichever
	/// registration ran last rather than on its own.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	public AwsSnsTopicEncryptionService(
		IAmazonSimpleNotificationService snsClient,
		IOptionsMonitor<AwsSnsOptions> options,
		string transportName,
		ILogger<AwsSnsTopicEncryptionService> logger)
	{
		_snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_transportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var options = _options.Get(_transportName);

		if (!options.EnableEncryption)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(options.KmsMasterKeyId))
		{
			throw new InvalidOperationException(
				"Server-side encryption was requested for the AWS SNS transport but no KMS key was supplied. " +
				"Set the KMS master key id when enabling encryption, or disable encryption.");
		}

		if (string.IsNullOrWhiteSpace(options.TopicArn))
		{
			throw new InvalidOperationException(
				"Server-side encryption was requested for the AWS SNS transport but no topic ARN was supplied. " +
				"Set the topic ARN so the encryption key can be applied to the topic.");
		}

		LogTopicEncryptionApplying(options.TopicArn, options.KmsMasterKeyId);

		var request = new SetTopicAttributesRequest
		{
			TopicArn = options.TopicArn,
			AttributeName = KmsMasterKeyIdAttribute,
			AttributeValue = options.KmsMasterKeyId,
		};

		_ = await _snsClient.SetTopicAttributesAsync(request, cancellationToken).ConfigureAwait(false);

		LogTopicEncryptionApplied(options.TopicArn);
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(AwsSqsEventId.ProvisioningTopicEncryptionApplying, LogLevel.Information,
		"AWS SNS: applying server-side encryption to topic {TopicArn} using key {KmsMasterKeyId}")]
	private partial void LogTopicEncryptionApplying(string topicArn, string kmsMasterKeyId);

	[LoggerMessage(AwsSqsEventId.ProvisioningTopicEncryptionApplied, LogLevel.Information,
		"AWS SNS: server-side encryption applied to topic {TopicArn}")]
	private partial void LogTopicEncryptionApplied(string topicArn);
}
