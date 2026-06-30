// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http;

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class GrpcTransportKeepAliveAndRetryShould
{
	[Fact]
	public void BuildKeepAliveHandlerWithConfiguredHttp2KeepAliveAndPoolingSettings()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			KeepAlivePingDelaySeconds = 45,
			KeepAlivePingTimeoutSeconds = 15,
			PooledConnectionIdleTimeoutSeconds = 600,
			EnableMultipleHttp2Connections = true,
		};

		// Act
		var handler = GrpcTransportServiceCollectionExtensions.BuildKeepAliveHandler(options);

		// Assert -- the channel now carries a SocketsHttpHandler with keep-alive + pooling tuning
		// (pre-fix: no HttpHandler was configured at all).
		handler.ShouldBeOfType<SocketsHttpHandler>();
		handler.KeepAlivePingDelay.ShouldBe(TimeSpan.FromSeconds(45));
		handler.KeepAlivePingTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		handler.PooledConnectionIdleTimeout.ShouldBe(TimeSpan.FromSeconds(600));
		handler.EnableMultipleHttp2Connections.ShouldBeTrue();
	}

	[Fact]
	public void BuildServiceConfigWithRetryMethodConfigWhenRetriesEnabled()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			EnableRetries = true,
			MaxRetryAttempts = 4,
			RetryInitialBackoffSeconds = 2,
			RetryMaxBackoffSeconds = 10,
			RetryBackoffMultiplier = 2,
		};

		// Act
		var serviceConfig = GrpcTransportServiceCollectionExtensions.BuildServiceConfig(options);

		// Assert -- a default retry ServiceConfig is now emitted (pre-fix: no ServiceConfig at all).
		serviceConfig.ShouldNotBeNull();
		var methodConfig = serviceConfig.MethodConfigs.ShouldHaveSingleItem();
		var retryPolicy = methodConfig.RetryPolicy.ShouldNotBeNull();
		retryPolicy.MaxAttempts.ShouldBe(4);
		retryPolicy.InitialBackoff.ShouldBe(TimeSpan.FromSeconds(2));
		retryPolicy.MaxBackoff.ShouldBe(TimeSpan.FromSeconds(10));
		retryPolicy.BackoffMultiplier.ShouldBe(2);
		retryPolicy.RetryableStatusCodes.ShouldContain(StatusCode.Unavailable);
		methodConfig.HedgingPolicy.ShouldBeNull();
	}

	[Fact]
	public void BuildServiceConfigReturnsNullWhenRetriesAndHedgingDisabled()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			EnableRetries = false,
			EnableHedging = false,
		};

		// Act
		var serviceConfig = GrpcTransportServiceCollectionExtensions.BuildServiceConfig(options);

		// Assert
		serviceConfig.ShouldBeNull();
	}

	[Fact]
	public void BuildServiceConfigWithHedgingMethodConfigWhenHedgingEnabled()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			EnableHedging = true,
			MaxRetryAttempts = 3,
			RetryInitialBackoffSeconds = 1,
		};

		// Act
		var serviceConfig = GrpcTransportServiceCollectionExtensions.BuildServiceConfig(options);

		// Assert -- hedging swaps RetryPolicy for a HedgingPolicy (gRPC permits only one of the two).
		serviceConfig.ShouldNotBeNull();
		var methodConfig = serviceConfig.MethodConfigs.ShouldHaveSingleItem();
		var hedgingPolicy = methodConfig.HedgingPolicy.ShouldNotBeNull();
		hedgingPolicy.MaxAttempts.ShouldBe(3);
		hedgingPolicy.NonFatalStatusCodes.ShouldContain(StatusCode.Unavailable);
		methodConfig.RetryPolicy.ShouldBeNull();
	}
}
