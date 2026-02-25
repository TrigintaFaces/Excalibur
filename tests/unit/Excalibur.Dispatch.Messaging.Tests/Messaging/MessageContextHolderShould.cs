// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageContextHolder"/>.
/// </summary>
/// <remarks>
/// Tests the thread-local storage for message context.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MessageContextHolderShould
{
	#region Current Property Tests

	[Fact]
	public void Current_DefaultIsNull()
	{
		// Arrange
		MessageContextHolder.Clear();

		// Assert
		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public void Current_CanBeSet()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();

		// Act
		MessageContextHolder.Current = context;

		// Assert
		MessageContextHolder.Current.ShouldBe(context);

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public void Current_CanBeSetToNull()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context;

		// Act
		MessageContextHolder.Current = null;

		// Assert
		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public void Current_CanBeOverwritten()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context1 = A.Fake<IMessageContext>();
		var context2 = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context1;

		// Act
		MessageContextHolder.Current = context2;

		// Assert
		MessageContextHolder.Current.ShouldBe(context2);

		// Cleanup
		MessageContextHolder.Clear();
	}

	#endregion

	#region Clear Method Tests

	[Fact]
	public void Clear_SetsCurrentToNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context;

		// Act
		MessageContextHolder.Clear();

		// Assert
		MessageContextHolder.Current.ShouldBeNull();
	}

	[Fact]
	public void Clear_WhenAlreadyNull_DoesNotThrow()
	{
		// Arrange
		MessageContextHolder.Current = null;

		// Act & Assert
		Should.NotThrow(() => MessageContextHolder.Clear());
	}

	[Fact]
	public void Clear_CalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context;

		// Act & Assert
		MessageContextHolder.Clear();
		Should.NotThrow(() => MessageContextHolder.Clear());
		Should.NotThrow(() => MessageContextHolder.Clear());
	}

	#endregion

	#region Thread Isolation Tests

	[Fact]
	public async Task Current_IsIsolatedPerAsyncContext()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context1 = A.Fake<IMessageContext>();
		var context2 = A.Fake<IMessageContext>();
		IMessageContext? capturedContext1 = null;
		IMessageContext? capturedContext2 = null;
		var firstContextSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondContextSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act
		var task1 = Task.Run(async () =>
		{
			MessageContextHolder.Current = context1;
			firstContextSet.TrySetResult();
			await secondContextSet.Task;
			capturedContext1 = MessageContextHolder.Current;
		});

		var task2 = Task.Run(async () =>
		{
			MessageContextHolder.Current = context2;
			secondContextSet.TrySetResult();
			await firstContextSet.Task;
			capturedContext2 = MessageContextHolder.Current;
		});

		await Task.WhenAll(task1, task2);

		// Assert - Each async context should have its own value
		capturedContext1.ShouldBe(context1);
		capturedContext2.ShouldBe(context2);

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public async Task Clear_OnlyAffectsCurrentAsyncContext()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();
		IMessageContext? capturedBeforeClear = null;
		IMessageContext? capturedAfterClear = null;
		var contextSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var clearCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act
		var task1 = Task.Run(async () =>
		{
			MessageContextHolder.Current = context;
			contextSet.TrySetResult();
			await clearCompleted.Task;
			capturedBeforeClear = MessageContextHolder.Current;
		});

		var task2 = Task.Run(async () =>
		{
			await contextSet.Task;
			MessageContextHolder.Clear();
			capturedAfterClear = MessageContextHolder.Current;
			clearCompleted.TrySetResult();
		});

		await Task.WhenAll(task1, task2);

		// Assert
		capturedBeforeClear.ShouldBe(context);
		capturedAfterClear.ShouldBeNull();

		// Cleanup
		MessageContextHolder.Clear();
	}

	#endregion

	#region Static Class Tests

	[Fact]
	public void IsStaticClass()
	{
		// Assert
		typeof(MessageContextHolder).IsAbstract.ShouldBeTrue();
		typeof(MessageContextHolder).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HasNoInstanceConstructors()
	{
		// Assert
		var constructors = typeof(MessageContextHolder).GetConstructors();
		constructors.Length.ShouldBe(0);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInMessageProcessingPipeline()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("test-message-id");

		// Act - Simulate setting context before processing
		MessageContextHolder.Current = context;

		// Assert - Context is available
		_ = MessageContextHolder.Current.ShouldNotBeNull();
		MessageContextHolder.Current.MessageId.ShouldBe("test-message-id");

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public async Task ContextFlowsThroughAsyncOperations()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("async-message-id");
		MessageContextHolder.Current = context;

		// Act & Assert - Context should flow through async operations
		var capturedId = await GetMessageIdFromCurrentContextAsync();
		capturedId.ShouldBe("async-message-id");

		// Cleanup
		MessageContextHolder.Clear();
	}

	private static async Task<string?> GetMessageIdFromCurrentContextAsync()
	{
		await Task.Yield();
		return MessageContextHolder.Current?.MessageId;
	}

	#endregion
}
