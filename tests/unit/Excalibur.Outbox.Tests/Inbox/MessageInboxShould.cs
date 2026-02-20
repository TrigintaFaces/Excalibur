// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- FakeItEasy fakes do not require disposal

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Use explicit alias to disambiguate from Excalibur.Outbox.InboxOptions
using DispatchInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="MessageInbox"/>.
/// Verifies inbox message processing, storage, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class MessageInboxShould : IAsyncDisposable
{
	private readonly IInboxStore _inboxStore;
	private readonly IInboxProcessor _processor;
	private readonly IJsonSerializer _serializer;
	private readonly IOptions<DispatchInboxOptions> _options;
	private readonly ILogger<MessageInbox> _logger;
	private MessageInbox? _sut;

	public MessageInboxShould()
	{
		_inboxStore = A.Fake<IInboxStore>();
		_processor = A.Fake<IInboxProcessor>();
		_serializer = A.Fake<IJsonSerializer>();
		_options = Options.Create(new DispatchInboxOptions());
		_logger = A.Fake<ILogger<MessageInbox>>();
	}

	public async ValueTask DisposeAsync()
	{
		if (_sut != null)
		{
			await _sut.DisposeAsync();
		}
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenInboxStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageInbox(null!, _processor, _serializer, _options, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenProcessorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageInbox(_inboxStore, null!, _serializer, _options, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSerializerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageInbox(_inboxStore, _processor, null!, _options, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageInbox(_inboxStore, _processor, _serializer, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MessageInbox(_inboxStore, _processor, _serializer, _options, null!));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Assert
		_sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateInstance_WithPayloadSerializer()
	{
		// Arrange
		var payloadSerializer = A.Fake<IPayloadSerializer>();

		// Act
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, payloadSerializer, _options, _logger);

		// Assert
		_sut.ShouldNotBeNull();
	}

	#endregion

	#region RunInboxDispatchAsync Tests

	[Fact]
	public async Task ThrowArgumentException_WhenDispatcherIdIsNull()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.RunInboxDispatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentException_WhenDispatcherIdIsEmpty()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.RunInboxDispatchAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentException_WhenDispatcherIdIsWhitespace()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.RunInboxDispatchAsync("   ", CancellationToken.None));
	}

	[Fact]
	public async Task InitializeProcessor_WithDispatcherId()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		const string dispatcherId = "test-dispatcher";

		// Act
		await _sut.RunInboxDispatchAsync(dispatcherId, CancellationToken.None);

		// Assert
		A.CallTo(() => _processor.Init(dispatcherId)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CallDispatchPendingMessagesAsync_OnProcessor()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		A.CallTo(() => _processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.Returns(5);

		// Act
		var result = await _sut.RunInboxDispatchAsync("dispatcher-1", CancellationToken.None);

		// Assert
		result.ShouldBe(5);
		A.CallTo(() => _processor.DispatchPendingMessagesAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowObjectDisposedException_WhenDisposed()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		await _sut.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _sut.RunInboxDispatchAsync("test", CancellationToken.None));

		// Clear reference since it's disposed
		_sut = null;
	}

	#endregion

	#region SaveMessageAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		var metadata = A.Fake<IMessageMetadata>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SaveMessageAsync<TestDispatchMessage>(null!, "ext-1", metadata, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentException_WhenExternalIdIsNull()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		var message = new TestDispatchMessage();
		var metadata = A.Fake<IMessageMetadata>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.SaveMessageAsync(message, null!, metadata, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentException_WhenExternalIdIsEmpty()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		var message = new TestDispatchMessage();
		var metadata = A.Fake<IMessageMetadata>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.SaveMessageAsync(message, "", metadata, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMetadataIsNull()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		var message = new TestDispatchMessage();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SaveMessageAsync(message, "ext-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task CallCreateEntryAsync_OnInboxStore()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		var message = new TestDispatchMessage();
		var metadata = CreateTestMetadata();

		A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.Returns(Task.FromResult("{\"test\":true}"));

		// Act
		await _sut.SaveMessageAsync(message, "ext-123", metadata, CancellationToken.None);

		// Assert
		A.CallTo(() => _inboxStore.CreateEntryAsync(
			"ext-123",
			A<string>._,
			A<string>._,
			A<byte[]>._,
			A<IDictionary<string, object>>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UsePayloadSerializer_WhenProvided()
	{
		// Arrange
		var payloadSerializer = A.Fake<IPayloadSerializer>();
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, payloadSerializer, _options, _logger);
		var message = new TestDispatchMessage();
		var metadata = CreateTestMetadata();

		A.CallTo(() => payloadSerializer.Serialize(A<object>._))
			.Returns([1, 2, 3]);

		// Act
		await _sut.SaveMessageAsync(message, "ext-456", metadata, CancellationToken.None);

		// Assert
		A.CallTo(() => payloadSerializer.Serialize(message))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _serializer.SerializeAsync(A<object>._, A<Type>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowObjectDisposedException_WhenDisposed_ForSaveMessage()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);
		await _sut.DisposeAsync();
		var message = new TestDispatchMessage();
		var metadata = CreateTestMetadata();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _sut.SaveMessageAsync(message, "ext-1", metadata, CancellationToken.None));

		// Clear reference since it's disposed
		_sut = null;
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeProcessor_WhenDisposed()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Act
		await _sut.DisposeAsync();

		// Assert
		A.CallTo(() => _processor.DisposeAsync()).MustHaveHappenedOnceExactly();

		// Clear reference since it's disposed
		_sut = null;
	}

	[Fact]
	public async Task DisposeStore_WhenAsyncDisposable()
	{
		// Arrange
		var asyncDisposableStore = A.Fake<IInboxStore>(x => x.Implements<IAsyncDisposable>());
		_sut = new MessageInbox(asyncDisposableStore, _processor, _serializer, _options, _logger);

		// Act
		await _sut.DisposeAsync();

		// Assert
		A.CallTo(() => ((IAsyncDisposable)asyncDisposableStore).DisposeAsync())
			.MustHaveHappenedOnceExactly();

		// Clear reference since it's disposed
		_sut = null;
	}

	[Fact]
	public async Task DisposeStore_WhenDisposable()
	{
		// Arrange
		var disposableStore = A.Fake<IInboxStore>(x => x.Implements<IDisposable>());
		_sut = new MessageInbox(disposableStore, _processor, _serializer, _options, _logger);

		// Act
		await _sut.DisposeAsync();

		// Assert
		A.CallTo(() => ((IDisposable)disposableStore).Dispose())
			.MustHaveHappenedOnceExactly();

		// Clear reference since it's disposed
		_sut = null;
	}

	[Fact]
	public async Task NotThrow_WhenDisposedMultipleTimes()
	{
		// Arrange
		_sut = new MessageInbox(_inboxStore, _processor, _serializer, _options, _logger);

		// Act & Assert - should not throw
		await Should.NotThrowAsync(async () =>
		{
			await _sut.DisposeAsync();
			await _sut.DisposeAsync();
			await _sut.DisposeAsync();
		});

		// Clear reference since it's disposed
		_sut = null;
	}

	#endregion

	#region Helper Methods

	private static IMessageMetadata CreateTestMetadata()
	{
		// Use a simple fake without explicit property setup
		// FakeItEasy returns default values for unconfigured properties
		return A.Fake<IMessageMetadata>();
	}

	#endregion

	#region Test Doubles

	private sealed class TestDispatchMessage : IDispatchMessage
	{
		public string Data { get; set; } = "test";
	}

	#endregion
}
