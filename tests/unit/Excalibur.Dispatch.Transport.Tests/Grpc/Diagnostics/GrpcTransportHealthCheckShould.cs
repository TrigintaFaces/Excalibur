// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc.Diagnostics;

using Grpc.Net.Client;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Transport.Tests.Grpc.Diagnostics;

/// <summary>
/// Unit tests for <see cref="GrpcTransportHealthCheck"/>.
/// Sprint 697 T.33: gRPC transport test coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportHealthCheckShould
{
	[Fact]
	public async Task ReturnUnhealthy_WhenChannelIsNull()
	{
		// Arrange
		var sut = new GrpcTransportHealthCheck(channel: null);

		// Act
		var result = await sut.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not registered");
	}

	[Fact]
	public async Task ReturnHealthy_WhenChannelIsConfigured()
	{
		// Arrange
		using var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sut = new GrpcTransportHealthCheck(channel);

		// Act
		var result = await sut.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("localhost:5001");
	}

	[Fact]
	public async Task ReturnHealthy_WhenChannelIsDisposedButTargetAccessible()
	{
		// Arrange -- GrpcChannel.Target remains accessible after Dispose
		// (ObjectDisposedException is thrown only on actual RPC calls, not Target access)
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		channel.Dispose();
		var sut = new GrpcTransportHealthCheck(channel);

		// Act
		var result = await sut.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert -- Target still works, so health check reports healthy
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("localhost:5001");
	}

	[Fact]
	public async Task DefaultToNullChannel_WhenNoParameterProvided()
	{
		// Arrange
		var sut = new GrpcTransportHealthCheck();

		// Act
		var result = await sut.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}
}
