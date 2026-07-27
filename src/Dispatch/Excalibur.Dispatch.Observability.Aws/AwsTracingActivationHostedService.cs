// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Activates the AWS tracing/metrics integration at application startup by invoking
/// <see cref="IAwsTracingIntegration.ConfigureXRayAsync"/> and
/// <see cref="IAwsTracingIntegration.ConfigureCloudWatchMetricsAsync"/>.
/// </summary>
/// <remarks>
/// Without this hosted service the registered <see cref="IAwsTracingIntegration"/> is never activated,
/// so the X-Ray activity listeners and CloudWatch meter listeners are never attached. Each
/// <c>Configure*Async</c> call self-gates on its <see cref="AwsObservabilityOptions"/> enable flag, so
/// activation is a no-op when the corresponding feature is disabled.
/// </remarks>
internal sealed class AwsTracingActivationHostedService(IAwsTracingIntegration tracingIntegration) : IHostedService
{
	private readonly IAwsTracingIntegration _tracingIntegration =
		tracingIntegration ?? throw new ArgumentNullException(nameof(tracingIntegration));

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		await _tracingIntegration.ConfigureXRayAsync(cancellationToken).ConfigureAwait(false);
		await _tracingIntegration.ConfigureCloudWatchMetricsAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
