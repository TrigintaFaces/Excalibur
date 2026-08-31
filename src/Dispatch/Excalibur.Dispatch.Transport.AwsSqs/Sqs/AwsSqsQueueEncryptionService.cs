// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Applies the requested server-side encryption key to the configured SQS queue during host start-up.
/// </summary>
/// <remarks>
/// <para>
/// Server-side encryption on SQS is an attribute of the queue, not of an individual send, so a transport
/// that only sends and receives cannot make an unencrypted queue encrypted. When encryption is requested
/// this service sets the queue's <c>KmsMasterKeyId</c> attribute so the key the operator named actually
/// reaches AWS instead of being held in configuration.
/// </para>
/// <para>
/// The service is inert unless encryption was requested. When it has been requested it is
/// <b>fail-closed</b>: a missing key, a missing queue URL, or a failure to apply the key aborts start-up
/// rather than letting the host run while sending to a queue the operator believes is encrypted. This
/// differs deliberately from the fail-open behaviour of the queue provisioning path, where a failure costs
/// throughput rather than confidentiality.
/// </para>
/// </remarks>
internal sealed partial class AwsSqsQueueEncryptionService : IHostedService
{
	private const string KmsMasterKeyIdAttribute = "KmsMasterKeyId";
	private const string KmsDataKeyReusePeriodAttribute = "KmsDataKeyReusePeriodSeconds";

	private readonly IAmazonSQS _sqsClient;
	private readonly IOptionsMonitor<AwsSqsOptions> _options;
	private readonly string _transportName;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsQueueEncryptionService"/> class.
	/// </summary>
	/// <param name="sqsClient">The AWS SQS client.</param>
	/// <param name="options">The SQS transport options, read by transport name.</param>
	/// <param name="transportName">
	/// The name this transport was registered under. The options are read by name so that a host running
	/// two named SQS transports applies each transport's own KMS key to its own queue, rather than
	/// whichever registration ran last.
	/// </param>
	/// <param name="logger">The logger instance.</param>
	public AwsSqsQueueEncryptionService(
		IAmazonSQS sqsClient,
		IOptionsMonitor<AwsSqsOptions> options,
		string transportName,
		ILogger<AwsSqsQueueEncryptionService> logger)
	{
		_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
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
				"Server-side encryption was requested for the AWS SQS transport but no KMS key was supplied. " +
				"Set the KMS master key id when enabling encryption, or disable encryption.");
		}

		if (options.QueueUrl is null)
		{
			throw new InvalidOperationException(
				"Server-side encryption was requested for the AWS SQS transport but no queue URL was supplied. " +
				"Set the queue URL so the encryption key can be applied to the queue.");
		}

		var queueUrl = options.QueueUrl.ToString();

		LogQueueEncryptionApplying(queueUrl, options.KmsMasterKeyId);

		var request = new SetQueueAttributesRequest
		{
			QueueUrl = queueUrl,
			Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				[KmsMasterKeyIdAttribute] = options.KmsMasterKeyId,
				[KmsDataKeyReusePeriodAttribute] =
					options.KmsDataKeyReusePeriodSeconds.ToString(CultureInfo.InvariantCulture),
			},
		};

		_ = await _sqsClient.SetQueueAttributesAsync(request, cancellationToken).ConfigureAwait(false);

		LogQueueEncryptionApplied(queueUrl);
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(AwsSqsEventId.ProvisioningQueueEncryptionApplying, LogLevel.Information,
		"AWS SQS: applying server-side encryption to queue {QueueUrl} using key {KmsMasterKeyId}")]
	private partial void LogQueueEncryptionApplying(string queueUrl, string kmsMasterKeyId);

	[LoggerMessage(AwsSqsEventId.ProvisioningQueueEncryptionApplied, LogLevel.Information,
		"AWS SQS: server-side encryption applied to queue {QueueUrl}")]
	private partial void LogQueueEncryptionApplied(string queueUrl);
}
