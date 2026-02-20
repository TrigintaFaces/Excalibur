// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Handlers;

/// <summary>
/// Unit tests for the HandlerRegistry class covering registration, resolution, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public class HandlerRegistryShould : UnitTestBase
{
	private readonly HandlerRegistry _sut = new();

	#region Single Handler Registration Tests

	[Fact]
	public void Register_SingleHandler_StoresHandlerEntry()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var handlerType = typeof(TestCommandHandler);

		// Act
		_sut.Register(messageType, handlerType, expectsResponse: false);

		// Assert
		var result = _sut.TryGetHandler(messageType, out var entry);
		result.ShouldBeTrue();
		entry.MessageType.ShouldBe(messageType);
		entry.HandlerType.ShouldBe(handlerType);
		entry.ExpectsResponse.ShouldBeFalse();
	}

	[Fact]
	public void Register_HandlerWithResponse_SetsExpectsResponseTrue()
	{
		// Arrange
		var messageType = typeof(TestQuery);
		var handlerType = typeof(TestQueryHandler);

		// Act
		_sut.Register(messageType, handlerType, expectsResponse: true);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void Register_HandlerWithoutResponse_SetsExpectsResponseFalse()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var handlerType = typeof(TestCommandHandler);

		// Act
		_sut.Register(messageType, handlerType, expectsResponse: false);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.ExpectsResponse.ShouldBeFalse();
	}

	[Fact]
	public void Register_MultipleDistinctHandlers_StoresAllEntries()
	{
		// Arrange & Act
		_sut.Register(typeof(TestCommand), typeof(TestCommandHandler), false);
		_sut.Register(typeof(TestQuery), typeof(TestQueryHandler), true);
		_sut.Register(typeof(TestEvent), typeof(TestEventHandler), false);

		// Assert
		var all = _sut.GetAll();
		all.Count.ShouldBe(3);
	}

	#endregion Single Handler Registration Tests

	#region Duplicate Handler Registration Tests

	[Fact]
	public void Register_SameMessageTypeTwice_OverwritesPreviousHandler()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var firstHandler = typeof(TestCommandHandler);
		var secondHandler = typeof(AlternateCommandHandler);

		// Act
		_sut.Register(messageType, firstHandler, false);
		_sut.Register(messageType, secondHandler, false);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.HandlerType.ShouldBe(secondHandler);
	}

	[Fact]
	public void Register_SameMessageTypeWithDifferentExpectsResponse_OverwritesWithNewValue()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var handlerType = typeof(TestCommandHandler);

		// Act
		_sut.Register(messageType, handlerType, expectsResponse: false);
		_sut.Register(messageType, handlerType, expectsResponse: true);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void Register_SameMessageTypeTwice_MaintainsSingleEntry()
	{
		// Arrange
		var messageType = typeof(TestCommand);

		// Act
		_sut.Register(messageType, typeof(TestCommandHandler), false);
		_sut.Register(messageType, typeof(AlternateCommandHandler), false);

		// Assert
		var all = _sut.GetAll();
		all.Count.ShouldBe(1);
	}

	#endregion Duplicate Handler Registration Tests

	#region Handler Resolution Tests

	[Fact]
	public void TryGetHandler_RegisteredMessageType_ReturnsTrue()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		_sut.Register(messageType, typeof(TestCommandHandler), false);

		// Act
		var result = _sut.TryGetHandler(messageType, out _);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TryGetHandler_RegisteredMessageType_OutputsCorrectEntry()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var handlerType = typeof(TestCommandHandler);
		_sut.Register(messageType, handlerType, true);

		// Act
		_ = _sut.TryGetHandler(messageType, out var entry);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageType.ShouldBe(messageType);
		entry.HandlerType.ShouldBe(handlerType);
		entry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void TryGetHandler_UnregisteredMessageType_ReturnsFalse()
	{
		// Arrange
		_sut.Register(typeof(TestCommand), typeof(TestCommandHandler), false);

		// Act
		var result = _sut.TryGetHandler(typeof(TestQuery), out _);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void TryGetHandler_EmptyRegistry_ReturnsFalse()
	{
		// Act
		var result = _sut.TryGetHandler(typeof(TestCommand), out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion Handler Resolution Tests

	#region GetAll Tests

	[Fact]
	public void GetAll_EmptyRegistry_ReturnsEmptyList()
	{
		// Act
		var result = _sut.GetAll();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetAll_WithRegistrations_ReturnsAllEntries()
	{
		// Arrange
		_sut.Register(typeof(TestCommand), typeof(TestCommandHandler), false);
		_sut.Register(typeof(TestQuery), typeof(TestQueryHandler), true);

		// Act
		var result = _sut.GetAll();

		// Assert
		result.Count.ShouldBe(2);
	}

	[Fact]
	public void GetAll_ReturnsReadOnlyList()
	{
		// Arrange
		_sut.Register(typeof(TestCommand), typeof(TestCommandHandler), false);

		// Act
		var result = _sut.GetAll();

		// Assert
		_ = result.ShouldBeAssignableTo<IReadOnlyList<HandlerRegistryEntry>>();
	}

	[Fact]
	public void GetAll_ReturnsSnapshot_NotAffectedBySubsequentRegistrations()
	{
		// Arrange
		_sut.Register(typeof(TestCommand), typeof(TestCommandHandler), false);
		var snapshot = _sut.GetAll();

		// Act
		_sut.Register(typeof(TestQuery), typeof(TestQueryHandler), true);

		// Assert
		snapshot.Count.ShouldBe(1);
		_sut.GetAll().Count.ShouldBe(2);
	}

	#endregion GetAll Tests

	#region Generic Handler Registration Tests

	[Fact]
	public void Register_GenericHandlerType_StoresCorrectly()
	{
		// Arrange
		var messageType = typeof(GenericCommand<string>);
		var handlerType = typeof(GenericCommandHandler<string>);

		// Act
		_sut.Register(messageType, handlerType, false);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.HandlerType.ShouldBe(handlerType);
	}

	[Fact]
	public void Register_OpenGenericTypes_StoresCorrectly()
	{
		// Arrange
		var messageType = typeof(GenericCommand<>);
		var handlerType = typeof(GenericCommandHandler<>);

		// Act
		_sut.Register(messageType, handlerType, false);

		// Assert
		_ = _sut.TryGetHandler(messageType, out var entry);
		entry.HandlerType.ShouldBe(handlerType);
	}

	[Fact]
	public void TryGetHandler_ClosedGenericNotRegistered_WhenOpenGenericExists_ReturnsFalse()
	{
		// Arrange
		_sut.Register(typeof(GenericCommand<>), typeof(GenericCommandHandler<>), false);

		// Act
		var result = _sut.TryGetHandler(typeof(GenericCommand<string>), out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion Generic Handler Registration Tests

	#region HandlerRegistryEntry Tests

	[Fact]
	public void HandlerRegistryEntry_Constructor_SetsAllProperties()
	{
		// Arrange
		var messageType = typeof(TestCommand);
		var handlerType = typeof(TestCommandHandler);
		var expectsResponse = true;

		// Act
		var entry = new HandlerRegistryEntry(messageType, handlerType, expectsResponse);

		// Assert
		entry.MessageType.ShouldBe(messageType);
		entry.HandlerType.ShouldBe(handlerType);
		entry.ExpectsResponse.ShouldBe(expectsResponse);
	}

	#endregion HandlerRegistryEntry Tests

	#region Thread Safety Tests

	[Fact]
	public async Task Register_ConcurrentRegistrations_AllSucceed()
	{
		// Arrange
		var tasks = new List<Task>();

		// Act
		foreach (var i in Enumerable.Range(0, 100))
		{
			var index = i;
			tasks.Add(Task.Run(() => _sut.Register(
				typeof(TestCommand),
				index % 2 == 0 ? typeof(TestCommandHandler) : typeof(AlternateCommandHandler),
				false)));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should have exactly one entry (last write wins)
		var all = _sut.GetAll();
		all.Count.ShouldBe(1);
	}

	#endregion Thread Safety Tests

	#region Test Fixtures

	private sealed class TestCommand
	{ }

	private sealed class TestQuery
	{ }

	private sealed class TestEvent
	{ }

	private sealed class TestCommandHandler
	{
		public Task HandleAsync(TestCommand command, CancellationToken ct) => Task.CompletedTask;
	}

	private sealed class AlternateCommandHandler
	{
		public Task HandleAsync(TestCommand command, CancellationToken ct) => Task.CompletedTask;
	}

	private sealed class TestQueryHandler
	{
		public Task<string> HandleAsync(TestQuery query, CancellationToken ct) => Task.FromResult("result");
	}

	private sealed class TestEventHandler
	{
		public Task HandleAsync(TestEvent @event, CancellationToken ct) => Task.CompletedTask;
	}

	private sealed class GenericCommand<T>
	{ }

	private sealed class GenericCommandHandler<T>
	{
		public Task HandleAsync(GenericCommand<T> command, CancellationToken ct) => Task.CompletedTask;
	}

	#endregion Test Fixtures
}
