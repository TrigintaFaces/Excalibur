// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="UpcastingMessageBusDecorator"/>.
/// Verifies version transformation, passthrough behavior, and observability tracking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Excalibur.Dispatch.Messaging")]
[Trait("Priority", "1")]
public sealed class UpcastingMessageBusDecoratorShould : UnitTestBase
{
	private readonly IMessageBus _innerBus;
	private readonly IUpcastingPipeline _pipeline;
	private readonly UpcastingMessageBusDecorator _sut;

	public UpcastingMessageBusDecoratorShould()
	{
		_innerBus = A.Fake<IMessageBus>();
		_pipeline = A.Fake<IUpcastingPipeline>();
		_sut = new UpcastingMessageBusDecorator(_innerBus, _pipeline);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenInnerBusIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new UpcastingMessageBusDecorator(null!, _pipeline));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenPipelineIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new UpcastingMessageBusDecorator(_innerBus, null!));
	}

	#endregion

	#region PublishAsync (IDispatchAction) Tests

	[Fact]
	public async Task PassActionThroughWithoutUpcasting()
	{
		// Arrange
		var action = new TestAction();
		var context = A.Fake<IMessageContext>();

		// Act
		await _sut.PublishAsync(action, context, CancellationToken.None);

		// Assert - action should pass through to inner bus without upcasting
		A.CallTo(() => _innerBus.PublishAsync(action, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _pipeline.Upcast(A<IDispatchMessage>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region PublishAsync (IDispatchDocument) Tests

	[Fact]
	public async Task PassDocumentThroughWithoutUpcasting()
	{
		// Arrange
		var document = new TestDocument();
		var context = A.Fake<IMessageContext>();

		// Act
		await _sut.PublishAsync(document, context, CancellationToken.None);

		// Assert - document should pass through to inner bus without upcasting
		A.CallTo(() => _innerBus.PublishAsync(document, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _pipeline.Upcast(A<IDispatchMessage>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region PublishAsync (IDispatchEvent) Tests

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenEventIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.PublishAsync((IDispatchEvent)null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenContextIsNullForEvent()
	{
		// Arrange
		var evt = new TestEvent();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.PublishAsync(evt, null!, CancellationToken.None));
	}

	[Fact]
	public async Task PassNonVersionedEventThroughWithoutUpcasting()
	{
		// Arrange
		var evt = new TestEvent(); // Not IVersionedMessage
		var context = A.Fake<IMessageContext>();

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - non-versioned event should pass through
		A.CallTo(() => _innerBus.PublishAsync(evt, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _pipeline.Upcast(A<IDispatchMessage>._))
			.MustNotHaveHappened();
		A.CallTo(() => _pipeline.GetLatestVersion(A<string>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PassVersionedEventAtLatestVersionThroughWithoutUpcasting()
	{
		// Arrange
		var evt = new VersionedEventV2(); // Version = 2
		var context = CreateFakeContextWithItems();

		A.CallTo(() => _pipeline.GetLatestVersion(evt.MessageType))
			.Returns(2); // Already at latest

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - should pass through without upcasting
		A.CallTo(() => _innerBus.PublishAsync(evt, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _pipeline.Upcast(A<IDispatchMessage>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PassVersionedEventThroughWhenNoVersionsRegistered()
	{
		// Arrange
		var evt = new VersionedEventV1(); // Version = 1
		var context = CreateFakeContextWithItems();

		A.CallTo(() => _pipeline.GetLatestVersion(evt.MessageType))
			.Returns(0); // No versions registered

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - should pass through when no latest version known
		A.CallTo(() => _innerBus.PublishAsync(evt, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _pipeline.Upcast(A<IDispatchMessage>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task UpcastVersionedEventWhenNotAtLatestVersion()
	{
		// Arrange
		var evt = new VersionedEventV1(); // Version = 1
		var upcastedEvt = new VersionedEventV2(); // Version = 2
		var context = CreateFakeContextWithItems();

		A.CallTo(() => _pipeline.GetLatestVersion(evt.MessageType))
			.Returns(2); // Latest is v2
		A.CallTo(() => _pipeline.Upcast(evt))
			.Returns(upcastedEvt);

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - should upcast and publish upcasted event
		A.CallTo(() => _pipeline.Upcast(evt))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _innerBus.PublishAsync(upcastedEvt, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SetObservabilityContextItemsWhenUpcasting()
	{
		// Arrange
		var evt = new VersionedEventV1(); // Version = 1
		var upcastedEvt = new VersionedEventV2(); // Version = 2
		var items = new Dictionary<string, object>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(items);

		A.CallTo(() => _pipeline.GetLatestVersion(evt.MessageType))
			.Returns(2);
		A.CallTo(() => _pipeline.Upcast(evt))
			.Returns(upcastedEvt);

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - context should have observability items
		items["Dispatch:OriginalMessageType"].ShouldBe(typeof(VersionedEventV1));
		items["Dispatch:UpcastedMessageType"].ShouldBe(typeof(VersionedEventV2));
		items["Dispatch:OriginalVersion"].ShouldBe(1);
		items["Dispatch:UpcastedVersion"].ShouldBe(2);
	}

	[Fact]
	public async Task UpcastMultipleVersionHops()
	{
		// Arrange
		var evt = new VersionedEventV1(); // Version = 1
		var upcastedEvt = new VersionedEventV3(); // Version = 3
		var context = CreateFakeContextWithItems();

		A.CallTo(() => _pipeline.GetLatestVersion(evt.MessageType))
			.Returns(3); // Latest is v3
		A.CallTo(() => _pipeline.Upcast(evt))
			.Returns(upcastedEvt); // Pipeline handles multi-hop

		// Act
		await _sut.PublishAsync(evt, context, CancellationToken.None);

		// Assert - should upcast and publish
		A.CallTo(() => _pipeline.Upcast(evt))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _innerBus.PublishAsync(upcastedEvt, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Helper Methods

	private static IMessageContext CreateFakeContextWithItems()
	{
		var items = new Dictionary<string, object>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(items);
		return context;
	}

	#endregion

	#region Test Doubles

	private sealed class TestAction : IDispatchAction
	{
	}

	private sealed class TestDocument : IDispatchDocument
	{
	}

	private sealed class TestEvent : IDispatchEvent
	{
	}

	private sealed class VersionedEventV1 : IDispatchEvent, IVersionedMessage
	{
		public int Version => 1;
		public string MessageType => "TestVersionedEvent";
	}

	private sealed class VersionedEventV2 : IDispatchEvent, IVersionedMessage
	{
		public int Version => 2;
		public string MessageType => "TestVersionedEvent";
	}

	private sealed class VersionedEventV3 : IDispatchEvent, IVersionedMessage
	{
		public int Version => 3;
		public string MessageType => "TestVersionedEvent";
	}

	#endregion
}
