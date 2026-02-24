// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

namespace Excalibur.Dispatch.Tests.Timing;

/// <summary>
/// Depth tests for <see cref="TimeoutOperationToken"/>.
/// Covers construction, completion, disposal, elapsed tracking,
/// and edge cases (double-dispose, complete after dispose).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimeoutOperationTokenShould
{
	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Arrange & Act
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);

		// Assert
		token.OperationId.ShouldNotBe(Guid.Empty);
		token.OperationType.ShouldBe(TimeoutOperationType.Handler);
		token.Context.ShouldBeNull();
		token.StartTime.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		token.IsCompleted.ShouldBeFalse();
		token.IsSuccessful.ShouldBeNull();
		token.HasTimedOut.ShouldBeNull();
	}

	[Fact]
	public void AcceptOptionalContext()
	{
		// Arrange
		var context = new TimeoutContext
		{
			HandlerType = typeof(string),
			MessageType = typeof(int),
		};

		// Act
		var token = new TimeoutOperationToken(TimeoutOperationType.Serialization, context);

		// Assert
		token.Context.ShouldNotBeNull();
		token.Context.HandlerType.ShouldBe(typeof(string));
		token.Context.MessageType.ShouldBe(typeof(int));
	}

	[Fact]
	public void TrackElapsedTime()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);

		// Act — small delay to ensure elapsed > 0
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		// Assert
		token.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CompleteWithSuccess()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);

		// Act
		token.Complete(success: true, timedOut: false);

		// Assert
		token.IsCompleted.ShouldBeTrue();
		token.IsSuccessful.ShouldBe(true);
		token.HasTimedOut.ShouldBe(false);
	}

	[Fact]
	public void CompleteWithTimeout()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Transport);

		// Act
		token.Complete(success: false, timedOut: true);

		// Assert
		token.IsCompleted.ShouldBeTrue();
		token.IsSuccessful.ShouldBe(false);
		token.HasTimedOut.ShouldBe(true);
	}

	[Fact]
	public void MarkAsCompletedOnDispose()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);
		token.IsCompleted.ShouldBeFalse();

		// Act
		token.Dispose();

		// Assert — dispose marks as completed with success=false, timedOut=false
		token.IsCompleted.ShouldBeTrue();
		token.IsSuccessful.ShouldBe(false);
		token.HasTimedOut.ShouldBe(false);
	}

	[Fact]
	public void NotOverwriteCompletionOnDispose()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);
		token.Complete(success: true, timedOut: false);

		// Act
		token.Dispose();

		// Assert — original completion state preserved
		token.IsSuccessful.ShouldBe(true);
	}

	[Fact]
	public void HandleDoubleDisposeSafely()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);

		// Act & Assert — should not throw
		token.Dispose();
		token.Dispose();
	}

	[Fact]
	public void IgnoreCompleteAfterDispose()
	{
		// Arrange
		var token = new TimeoutOperationToken(TimeoutOperationType.Handler);
		token.Dispose();

		// Act — Complete after dispose is ignored
		token.Complete(success: true, timedOut: false);

		// Assert — state from dispose remains
		token.IsSuccessful.ShouldBe(false);
	}
}
