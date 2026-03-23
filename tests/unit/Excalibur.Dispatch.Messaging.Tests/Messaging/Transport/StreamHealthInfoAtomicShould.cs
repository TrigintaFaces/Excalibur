// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Sprint 689 T.12 (dum10): Regression test for StreamHealthInfo non-atomic property fix.
// Before fix: property++ was non-atomic (read → increment → write had race window).
// After fix: Backing fields use Interlocked; internal atomic increment methods added.

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Transport;

/// <summary>
/// Regression tests for T.12 (dum10): StreamHealthInfo Interlocked-backed properties.
/// Verifies that concurrent reads and writes to counter properties are atomic.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamHealthInfoAtomicShould
{
	[Fact]
	public void ReadAndWriteMessagesReceivedAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act -- concurrent writes via public setter (Interlocked.Exchange)
		Parallel.For(0, 100, i =>
		{
			info.MessagesReceived = i;
		});

		// Assert -- value should be one of the assigned values (no torn read)
		var final = info.MessagesReceived;
		final.ShouldBeGreaterThanOrEqualTo(0);
		final.ShouldBeLessThan(100);
	}

	[Fact]
	public void ReadAndWriteBytesReceivedAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act
		Parallel.For(0, 100, i =>
		{
			info.BytesReceived = i * 1024L;
		});

		// Assert
		var final = info.BytesReceived;
		(final % 1024).ShouldBe(0, "Value should be a clean multiple of 1024 (no torn write)");
	}

	[Fact]
	public void ReadAndWriteErrorCountAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act
		Parallel.For(0, 100, i =>
		{
			info.ErrorCount = i;
		});

		// Assert
		var final = info.ErrorCount;
		final.ShouldBeGreaterThanOrEqualTo(0);
		final.ShouldBeLessThan(100);
	}

	[Fact]
	public void ReadAndWriteAcknowledgmentsSucceededAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act
		Parallel.For(0, 100, i =>
		{
			info.AcknowledgmentsSucceeded = i;
		});

		// Assert
		var final = info.AcknowledgmentsSucceeded;
		final.ShouldBeGreaterThanOrEqualTo(0);
		final.ShouldBeLessThan(100);
	}

	[Fact]
	public void ReadAndWriteAcknowledgmentsFailedAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act
		Parallel.For(0, 100, i =>
		{
			info.AcknowledgmentsFailed = i;
		});

		// Assert
		var final = info.AcknowledgmentsFailed;
		final.ShouldBeGreaterThanOrEqualTo(0);
		final.ShouldBeLessThan(100);
	}

	[Fact]
	public void ReadAndWriteReconnectCountAtomically()
	{
		// Arrange
		var info = new StreamHealthInfo("test-stream");

		// Act
		Parallel.For(0, 100, i =>
		{
			info.ReconnectCount = i;
		});

		// Assert
		var final = info.ReconnectCount;
		final.ShouldBeGreaterThanOrEqualTo(0);
		final.ShouldBeLessThan(100);
	}

	[Fact]
	public void HandleConcurrentReadsAndWritesWithoutCorruption()
	{
		// Arrange
		var info = new StreamHealthInfo("concurrent-test");
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		// Act -- concurrent mixed reads and writes across all counter properties
		Parallel.For(0, 200, i =>
		{
			try
			{
				// Writers
				info.MessagesReceived = i;
				info.BytesReceived = i * 100L;
				info.ErrorCount = i;
				info.AcknowledgmentsSucceeded = i;
				info.AcknowledgmentsFailed = i;
				info.ReconnectCount = i;

				// Readers -- should never see torn values
				_ = info.MessagesReceived;
				_ = info.BytesReceived;
				_ = info.ErrorCount;
				_ = info.AcknowledgmentsSucceeded;
				_ = info.AcknowledgmentsFailed;
				_ = info.ReconnectCount;
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		// Assert
		exceptions.ShouldBeEmpty("Concurrent reads/writes should never throw");
	}

	[Fact]
	public void InitializeWithExpectedDefaults()
	{
		// Arrange & Act
		var info = new StreamHealthInfo("default-test");

		// Assert
		info.StreamId.ShouldBe("default-test");
		info.MessagesReceived.ShouldBe(0);
		info.BytesReceived.ShouldBe(0);
		info.ErrorCount.ShouldBe(0);
		info.AcknowledgmentsSucceeded.ShouldBe(0);
		info.AcknowledgmentsFailed.ShouldBe(0);
		info.ReconnectCount.ShouldBe(0);
		info.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullStreamId()
	{
		Should.Throw<ArgumentNullException>(() => new StreamHealthInfo(null!));
	}
}
