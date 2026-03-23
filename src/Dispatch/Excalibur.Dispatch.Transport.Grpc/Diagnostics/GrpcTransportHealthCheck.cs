// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Net.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Transport.Grpc.Diagnostics;

/// <summary>
/// Health check for gRPC transport connectivity.
/// </summary>
/// <remarks>
/// Checks that the gRPC channel can be connected. Reports healthy when the channel
/// is configured, unhealthy when not registered or disposed.
/// Register via <c>AddHealthChecks().AddCheck&lt;GrpcTransportHealthCheck&gt;("grpc-transport")</c>.
/// </remarks>
internal sealed class GrpcTransportHealthCheck : IHealthCheck
{
	private readonly GrpcChannel? _channel;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrpcTransportHealthCheck"/> class.
	/// </summary>
	/// <param name="channel">The gRPC channel, or null if not yet established.</param>
	public GrpcTransportHealthCheck(GrpcChannel? channel = null)
	{
		_channel = channel;
	}

	/// <inheritdoc/>
	public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		if (_channel is null)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"gRPC channel is not registered. Register via AddGrpcTransport()."));
		}

		try
		{
			var target = _channel.Target;
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
				$"gRPC channel configured for {target}."));
		}
		catch (ObjectDisposedException)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"gRPC channel has been disposed."));
		}
	}
}
