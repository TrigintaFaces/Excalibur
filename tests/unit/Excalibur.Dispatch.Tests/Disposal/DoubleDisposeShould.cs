// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Channels;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Disposal;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class DoubleDisposeShould
{
	[Fact]
	public void NotThrowForDispatchChannel()
	{
		// Arrange
		var options = new DispatchChannelOptions { Mode = ChannelMode.Unbounded };
		var channel = new DispatchChannel<string>(options);

		// Act & Assert -- double dispose must not throw
		channel.Dispose();
		Should.NotThrow(() => channel.Dispose());
	}

	[Fact]
	public void NotThrowForInMemoryDeduplicator()
	{
		// Arrange
		var deduplicator = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			cleanupInterval: TimeSpan.FromHours(1));

		// Act & Assert
		deduplicator.Dispose();
		Should.NotThrow(() => deduplicator.Dispose());
	}

	[Fact]
	public void NotThrowForTimeoutOperationToken()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);

		// Act & Assert
		token.Dispose();
		Should.NotThrow(() => token.Dispose());
	}

	[Fact]
	public void NotThrowForDispatchTelemetryProvider()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions
		{
			ServiceName = "test",
			ServiceVersion = "1.0.0",
		});
		var provider = new DispatchTelemetryProvider(options);

		// Act & Assert
		provider.Dispose();
		Should.NotThrow(() => provider.Dispose());
	}

	[Fact]
	public void NotThrowForDispatchChannelBounded()
	{
		// Arrange -- bounded channel variant
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 100,
		};
		var channel = new DispatchChannel<int>(options);

		// Act & Assert
		channel.Dispose();
		Should.NotThrow(() => channel.Dispose());
	}

	[Fact]
	public void PreserveDisposedStateAcrossMultipleDisposes()
	{
		// Arrange
		var deduplicator = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			cleanupInterval: TimeSpan.FromHours(1));

		// Act -- dispose twice
		deduplicator.Dispose();
		deduplicator.Dispose();

		// Assert -- operations after dispose should handle gracefully
		// The deduplicator should be in a consistent disposed state
		// (internal _disposed flag prevents re-running cleanup logic)
		Should.NotThrow(() => deduplicator.Dispose());
	}

	[Fact]
	public void NotThrowWhenDisposingMultipleTimesInParallel()
	{
		// Arrange
		var options = new DispatchChannelOptions { Mode = ChannelMode.Unbounded };
		var channel = new DispatchChannel<string>(options);

		// Act & Assert -- concurrent dispose should not throw
		Should.NotThrow(() =>
			Parallel.For(0, 10, _ => channel.Dispose()));
	}

	[Fact]
	public void NotThrowForInMemoryDeduplicatorConcurrentDispose()
	{
		// Arrange
		var deduplicator = new InMemoryDeduplicator(
			NullLogger<InMemoryDeduplicator>.Instance,
			cleanupInterval: TimeSpan.FromHours(1));

		// Act & Assert -- concurrent dispose should not throw
		Should.NotThrow(() =>
			Parallel.For(0, 10, _ => deduplicator.Dispose()));
	}
}
