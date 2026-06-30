// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Runs the opt-in AWS SQS/SNS provisioning once during host start-up.
/// </summary>
/// <remarks>
/// Provisioning is fail-open by default: a failure (for example a missing IAM permission) is logged
/// and start-up continues, so the absence of provisioning permissions never crashes the host.
/// </remarks>
internal sealed partial class AwsSqsProvisioningHostedService : IHostedService
{
	private readonly AwsSqsProvisioner _provisioner;
	private readonly AwsSqsTransportAdapterOptions _options;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsProvisioningHostedService"/> class.
	/// </summary>
	/// <param name="provisioner">The provisioner that performs the AWS calls.</param>
	/// <param name="options">The transport adapter options describing the desired topology.</param>
	/// <param name="logger">The logger instance.</param>
	public AwsSqsProvisioningHostedService(
		AwsSqsProvisioner provisioner,
		AwsSqsTransportAdapterOptions options,
		ILogger<AwsSqsProvisioningHostedService> logger)
	{
		_provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.Provisioning.Enabled)
		{
			return;
		}

		try
		{
			await _provisioner.ProvisionAsync(_options, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex) when (_options.Provisioning.FailOpen)
		{
			LogProvisioningFailed(_options.Name ?? string.Empty, ex);
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(AwsSqsEventId.ProvisioningFailed, LogLevel.Warning,
		"AWS SQS provisioning: failed for transport {TransportName}; continuing (fail-open)")]
	private partial void LogProvisioningFailed(string transportName, Exception exception);
}
