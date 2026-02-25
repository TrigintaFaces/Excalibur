// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageBusPublishingShould
{
	private readonly IPayloadSerializer _serializer = A.Fake<IPayloadSerializer>();
	private readonly IMessageContext _context = A.Fake<IMessageContext>();
	private string? _capturedRoutingKey;
	private byte[]? _capturedBody;

	public MessageBusPublishingShould()
	{
		A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._))
			.Returns([1, 2, 3]);
		A.CallTo(() => _context.Items)
			.Returns(new Dictionary<string, object>());
	}

	private Task TestPublishFunc(string routingKey, byte[] body, IMessageContext ctx, CancellationToken ct)
	{
		_capturedRoutingKey = routingKey;
		_capturedBody = body;
		return Task.CompletedTask;
	}

	[Fact]
	public async Task PublishActionWithTypeNameAsRoutingKey()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();

		// Act
		await MessageBusPublishing.PublishActionAsync(
			action, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldNotBeNull();
		_capturedBody.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task PublishActionWithExplicitRoutingKey()
	{
		// Arrange
		var action = A.Fake<IDispatchAction>();
		var items = new Dictionary<string, object> { ["RoutingKey"] = "custom-key" };
		A.CallTo(() => _context.Items).Returns(items);

		// Act
		await MessageBusPublishing.PublishActionAsync(
			action, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldBe("custom-key");
	}

	[Fact]
	public async Task ThrowWhenActionIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishActionAsync(
				null!, _context, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenActionContextIsNull()
	{
		// Act & Assert
		var action = A.Fake<IDispatchAction>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishActionAsync(
				action, null!, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenActionSerializerIsNull()
	{
		// Act & Assert
		var action = A.Fake<IDispatchAction>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishActionAsync(
				action, _context, TestPublishFunc, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenActionPublishFuncIsNull()
	{
		// Act & Assert
		var action = A.Fake<IDispatchAction>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishActionAsync(
				action, _context, null!, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task PublishEventWithTypeNameAsRoutingKey()
	{
		// Arrange
		var evt = A.Fake<IDispatchEvent>();

		// Act
		await MessageBusPublishing.PublishEventAsync(
			evt, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldNotBeNull();
		_capturedBody.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task PublishEventWithExplicitRoutingKey()
	{
		// Arrange
		var evt = A.Fake<IDispatchEvent>();
		var items = new Dictionary<string, object> { ["RoutingKey"] = "event-key" };
		A.CallTo(() => _context.Items).Returns(items);

		// Act
		await MessageBusPublishing.PublishEventAsync(
			evt, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldBe("event-key");
	}

	[Fact]
	public async Task ThrowWhenEventIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishEventAsync(
				null!, _context, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEventContextIsNull()
	{
		// Act & Assert
		var evt = A.Fake<IDispatchEvent>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishEventAsync(
				evt, null!, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEventSerializerIsNull()
	{
		// Act & Assert
		var evt = A.Fake<IDispatchEvent>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishEventAsync(
				evt, _context, TestPublishFunc, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEventPublishFuncIsNull()
	{
		// Act & Assert
		var evt = A.Fake<IDispatchEvent>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishEventAsync(
				evt, _context, null!, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task PublishDocumentWithTypeNameAsRoutingKey()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();

		// Act
		await MessageBusPublishing.PublishDocumentAsync(
			doc, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldNotBeNull();
		_capturedBody.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task PublishDocumentWithExplicitRoutingKey()
	{
		// Arrange
		var doc = A.Fake<IDispatchDocument>();
		var items = new Dictionary<string, object> { ["RoutingKey"] = "doc-key" };
		A.CallTo(() => _context.Items).Returns(items);

		// Act
		await MessageBusPublishing.PublishDocumentAsync(
			doc, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert
		_capturedRoutingKey.ShouldBe("doc-key");
	}

	[Fact]
	public async Task ThrowWhenDocumentIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishDocumentAsync(
				null!, _context, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDocumentContextIsNull()
	{
		// Act & Assert
		var doc = A.Fake<IDispatchDocument>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishDocumentAsync(
				doc, null!, TestPublishFunc, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDocumentSerializerIsNull()
	{
		// Act & Assert
		var doc = A.Fake<IDispatchDocument>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishDocumentAsync(
				doc, _context, TestPublishFunc, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDocumentPublishFuncIsNull()
	{
		// Act & Assert
		var doc = A.Fake<IDispatchDocument>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => MessageBusPublishing.PublishDocumentAsync(
				doc, _context, null!, _serializer, CancellationToken.None));
	}

	[Fact]
	public async Task UseNonStringRoutingKeyFallsBackToTypeName()
	{
		// Arrange - RoutingKey exists but is not a string
		var action = A.Fake<IDispatchAction>();
		var items = new Dictionary<string, object> { ["RoutingKey"] = 42 };
		A.CallTo(() => _context.Items).Returns(items);

		// Act
		await MessageBusPublishing.PublishActionAsync(
			action, _context, TestPublishFunc, _serializer, CancellationToken.None);

		// Assert - should fall back to type name since RoutingKey is not a string
		_capturedRoutingKey.ShouldNotBeNull();
		_capturedRoutingKey.ShouldNotBe("42");
	}
}
