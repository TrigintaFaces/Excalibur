// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// T.9 Regression: MessagePump must implement IAsyncDisposable and await processing task.
/// Validates the fix for sync Cancel+Dispose without await that caused UnobservedTaskException.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessagePumpDisposalShould
{
	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert - T.9 regression: MessagePump must implement IAsyncDisposable
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(MessagePump<object>)).ShouldBeTrue();
	}

	[Fact]
	public async Task DisposeAsyncWithoutStarting_CompletesCleanly()
	{
		// Arrange - disposing without Start() should not throw
		var pump = new MessagePump<object>("test-pump", MemoryPool<byte>.Shared);

		// Act & Assert - no exception
		await pump.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsyncAfterStart_CompletesCleanly()
	{
		// Arrange - T.9 regression: DisposeAsync must await _processingTask
		var pump = new MessagePump<object>("test-pump", MemoryPool<byte>.Shared);
		pump.Start();

		// Act - DisposeAsync should await the processing task, not just sync Cancel+Dispose
		await pump.DisposeAsync();

		// Assert - no UnobservedTaskException (if we got here, the task was properly awaited)
	}

	[Fact]
	public async Task DoubleDisposeAsync_DoesNotThrow()
	{
		// Arrange
		var pump = new MessagePump<object>("test-pump", MemoryPool<byte>.Shared);
		pump.Start();

		// Act
		await pump.DisposeAsync();
		await pump.DisposeAsync(); // second call should be no-op

		// Assert - no exception on double dispose
	}

	[Fact]
	public void SyncDispose_CompletesCleanly()
	{
		// Arrange - sync Dispose path must also work without throwing
		var pump = new MessagePump<object>("test-pump", MemoryPool<byte>.Shared);
		pump.Start();

		// Act & Assert - no exception
		pump.Dispose();
	}
}
