// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatchMessageExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the dispatch message extension methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class DispatchMessageExtensionsShould
{
	#region IsAction Tests

	[Fact]
	public void IsAction_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IDispatchMessage message = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => message.IsAction());
	}

	[Fact]
	public void IsAction_WithActionMessage_ReturnsTrue()
	{
		// Arrange
		var message = new TestActionMessage();

		// Act
		var result = message.IsAction();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsAction_WithGenericActionMessage_ReturnsTrue()
	{
		// Arrange
		var message = new TestActionWithResultMessage();

		// Act
		var result = message.IsAction();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsAction_WithEventMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestEventMessage();

		// Act
		var result = message.IsAction();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsAction_WithDocumentMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestDocumentMessage();

		// Act
		var result = message.IsAction();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsEvent Tests

	[Fact]
	public void IsEvent_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IDispatchMessage message = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => message.IsEvent());
	}

	[Fact]
	public void IsEvent_WithEventMessage_ReturnsTrue()
	{
		// Arrange
		var message = new TestEventMessage();

		// Act
		var result = message.IsEvent();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsEvent_WithActionMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestActionMessage();

		// Act
		var result = message.IsEvent();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsEvent_WithDocumentMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestDocumentMessage();

		// Act
		var result = message.IsEvent();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsDocument Tests

	[Fact]
	public void IsDocument_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IDispatchMessage message = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => message.IsDocument());
	}

	[Fact]
	public void IsDocument_WithDocumentMessage_ReturnsTrue()
	{
		// Arrange
		var message = new TestDocumentMessage();

		// Act
		var result = message.IsDocument();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsDocument_WithActionMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestActionMessage();

		// Act
		var result = message.IsDocument();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsDocument_WithEventMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestEventMessage();

		// Act
		var result = message.IsDocument();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region ExpectsReturnValue Tests

	[Fact]
	public void ExpectsReturnValue_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		IDispatchMessage message = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => message.ExpectsReturnValue());
	}

	[Fact]
	public void ExpectsReturnValue_WithGenericActionMessage_ReturnsTrue()
	{
		// Arrange
		var message = new TestActionWithResultMessage();

		// Act
		var result = message.ExpectsReturnValue();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ExpectsReturnValue_WithNonGenericActionMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestActionMessage();

		// Act
		var result = message.ExpectsReturnValue();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ExpectsReturnValue_WithEventMessage_ReturnsFalse()
	{
		// Arrange
		var message = new TestEventMessage();

		// Act
		var result = message.ExpectsReturnValue();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Test Message Types

	private sealed class TestActionMessage : IDispatchAction
	{
		public object Body => this;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string MessageType { get; } = nameof(TestActionMessage);
		public Guid Id { get; } = Guid.NewGuid();
		public MessageKinds Kind { get; } = MessageKinds.Action;
	}

	private sealed class TestActionWithResultMessage : IDispatchAction<string>
	{
		public object Body => this;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string MessageType { get; } = nameof(TestActionWithResultMessage);
		public Guid Id { get; } = Guid.NewGuid();
		public MessageKinds Kind { get; } = MessageKinds.Action;
	}

	private sealed class TestEventMessage : IDispatchEvent
	{
		public object Body => this;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string MessageType { get; } = nameof(TestEventMessage);
		public Guid Id { get; } = Guid.NewGuid();
		public MessageKinds Kind { get; } = MessageKinds.Event;
	}

	private sealed class TestDocumentMessage : IDispatchDocument
	{
		public object Body => this;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public string MessageType { get; } = nameof(TestDocumentMessage);
		public Guid Id { get; } = Guid.NewGuid();
		public MessageKinds Kind { get; } = MessageKinds.Document;
	}

	#endregion
}
