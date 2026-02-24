// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageContextAccessor"/>.
/// </summary>
/// <remarks>
/// Tests the default implementation of IMessageContextAccessor.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MessageContextAccessorShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesInstance()
	{
		// Act
		var accessor = new MessageContextAccessor();

		// Assert
		_ = accessor.ShouldNotBeNull();
	}

	#endregion

	#region MessageContext Property Tests

	[Fact]
	public void MessageContext_DefaultIsNull()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();

		// Assert
		accessor.MessageContext.ShouldBeNull();
	}

	[Fact]
	public void MessageContext_CanBeSet()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		// Act
		accessor.MessageContext = context;

		// Assert
		accessor.MessageContext.ShouldBe(context);

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public void MessageContext_CanBeSetToNull()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();
		accessor.MessageContext = context;

		// Act
		accessor.MessageContext = null;

		// Assert
		accessor.MessageContext.ShouldBeNull();

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public void MessageContext_ReadsFromMessageContextHolder()
	{
		// Arrange
		MessageContextHolder.Clear();
		var context = A.Fake<IMessageContext>();
		MessageContextHolder.Current = context;
		var accessor = new MessageContextAccessor();

		// Assert
		accessor.MessageContext.ShouldBe(context);

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public void MessageContext_WritesToMessageContextHolder()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		// Act
		accessor.MessageContext = context;

		// Assert
		MessageContextHolder.Current.ShouldBe(context);

		// Cleanup
		MessageContextHolder.Clear();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMessageContextAccessor()
	{
		// Act
		var accessor = new MessageContextAccessor();

		// Assert
		_ = accessor.ShouldBeAssignableTo<IMessageContextAccessor>();
	}

	#endregion

	#region Multiple Accessor Tests

	[Fact]
	public void MultipleAccessors_ShareSameContext()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor1 = new MessageContextAccessor();
		var accessor2 = new MessageContextAccessor();
		var context = A.Fake<IMessageContext>();

		// Act
		accessor1.MessageContext = context;

		// Assert
		accessor2.MessageContext.ShouldBe(context);

		// Cleanup
		MessageContextHolder.Clear();
	}

	[Fact]
	public void ChangingContextInOneAccessor_AffectsOthers()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor1 = new MessageContextAccessor();
		var accessor2 = new MessageContextAccessor();
		var context1 = A.Fake<IMessageContext>();
		var context2 = A.Fake<IMessageContext>();

		// Act
		accessor1.MessageContext = context1;
		accessor2.MessageContext = context2;

		// Assert
		accessor1.MessageContext.ShouldBe(context2);
		accessor2.MessageContext.ShouldBe(context2);

		// Cleanup
		MessageContextHolder.Clear();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task MessageContext_IsIsolatedPerAsyncContext()
	{
		// Arrange
		MessageContextHolder.Clear();
		var accessor = new MessageContextAccessor();
		var context1 = A.Fake<IMessageContext>();
		var context2 = A.Fake<IMessageContext>();
		IMessageContext? capturedContext1 = null;
		IMessageContext? capturedContext2 = null;
		var firstContextSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondContextSet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act - Set different contexts in different async flows
		var task1 = Task.Run(async () =>
		{
			accessor.MessageContext = context1;
			firstContextSet.TrySetResult();
			await secondContextSet.Task;
			capturedContext1 = accessor.MessageContext;
		});

		var task2 = Task.Run(async () =>
		{
			accessor.MessageContext = context2;
			secondContextSet.TrySetResult();
			await firstContextSet.Task;
			capturedContext2 = accessor.MessageContext;
		});

		await Task.WhenAll(task1, task2);

		// Assert - Each async context should have its own value
		capturedContext1.ShouldBe(context1);
		capturedContext2.ShouldBe(context2);

		// Cleanup
		MessageContextHolder.Clear();
	}

	#endregion
}
