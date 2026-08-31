// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using MessagePack;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;
using MetadataRecord = Excalibur.Dispatch.Metadata.MessageMetadata;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Regression lock: an inbox entry written through a configured <see cref="IPayloadSerializer" /> must be
/// drainable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect.</b> The write side routes through <see cref="IPayloadSerializer" /> whenever one is
/// registered, and that contract is a magic-byte-prefixed binary encoding — MessagePack and Protobuf are
/// among the shipped implementations. The drain read the stored bytes back through the JSON reader, which
/// cannot parse MessagePack however the bytes are carried, so every such entry burned its retry budget and
/// dead-lettered without the handler ever running.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The serializer is the real MessagePack one obtained through its supported
/// registration, the payload is produced by the real <see cref="MessageInbox" /> write path, and the
/// assertion is that the dispatcher was <b>called</b> — not merely that the entry reached a terminal mark.
/// An arm asserting only the mark would pass on a drain that finalized an entry it never dispatched.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Feature", "Serialization")]
public sealed class InboxPayloadSerializerRoundTripShould
{
	private const string HandlerTypeName = "TestHandler";

	[Fact]
	public async Task DispatchAnEntryWrittenThroughTheConfiguredBinaryPayloadSerializer()
	{
		MessageTypeRegistry.RegisterType<BinaryInboxMessage>();

		await using var serializerProvider = BuildMessagePackProvider();
		var payloadSerializer = serializerProvider.GetRequiredService<IPayloadSerializer>();

		// Positive control: the registration actually took. Without it a green below could come from both
		// sides silently falling back to JSON, which is the composition that already worked.
		payloadSerializer.GetCurrentSerializerName().ShouldBe("MessagePack");
		payloadSerializer.GetCurrentSerializerId().ShouldBe(SerializerIds.MessagePack);

		var message = new BinaryInboxMessage { Id = "inbox-msgpack", Text = "payload" };
		var entry = await WriteThroughInboxAsync(message, payloadSerializer);

		// The stored bytes are MessagePack behind the serializer's magic byte, not UTF-8 JSON.
		entry.Payload[0].ShouldBe(SerializerIds.MessagePack);

		var dispatcher = CreateSucceedingDispatcher();
		var deadLetterQueue = A.Fake<IDeadLetterQueue>();
		_ = A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.Returns(Task.FromResult(Guid.NewGuid()));

		await using var dispatchProvider = BuildDispatchProvider(dispatcher);
		await using var processor = new InboxProcessor(
			SingleMessageOptions(),
			DrainStoreFor(entry),
			dispatchProvider,
			new DispatchJsonSerializer(),
			NullLogger<InboxProcessor>.Instance,
			deadLetterQueue: deadLetterQueue,
			payloadSerializer: payloadSerializer);
		processor.Init("dispatcher-1");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert -- the handler ran. Reaching a terminal mark without this call is exactly the failure the
		// binary payload produced before the drain read back through the serializer that wrote it.
		A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(m => ((BinaryInboxMessage)m).Text == "payload"),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		processed.ShouldBe(1);
		A.CallTo(() => deadLetterQueue.EnqueueAsync(
				A<IInboxMessage>._,
				A<DeadLetterReason>._,
				A<CancellationToken>._,
				A<Exception?>._,
				A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchAnEntryWrittenAsJsonWhileABinaryPayloadSerializerIsConfigured()
	{
		// A store written before a payload serializer was configured holds raw UTF-8 JSON. Those entries
		// carry no magic byte, so they must keep draining through the JSON reader rather than being handed
		// to a binary serializer that would reject them.
		MessageTypeRegistry.RegisterType<BinaryInboxMessage>();

		await using var serializerProvider = BuildMessagePackProvider();
		var payloadSerializer = serializerProvider.GetRequiredService<IPayloadSerializer>();

		var message = new BinaryInboxMessage { Id = "inbox-legacy-json", Text = "legacy" };
		var entry = await WriteThroughInboxAsync(message, payloadSerializer: null);
		entry.Payload[0].ShouldBe((byte)'{');

		var dispatcher = CreateSucceedingDispatcher();
		await using var dispatchProvider = BuildDispatchProvider(dispatcher);
		await using var processor = new InboxProcessor(
			SingleMessageOptions(),
			DrainStoreFor(entry),
			dispatchProvider,
			new DispatchJsonSerializer(),
			NullLogger<InboxProcessor>.Instance,
			payloadSerializer: payloadSerializer);
		processor.Init("dispatcher-1");

		// Act
		var processed = await processor.DispatchPendingMessagesAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>.That.Matches(m => ((BinaryInboxMessage)m).Text == "legacy"),
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		processed.ShouldBe(1);
	}

	private static ServiceProvider BuildMessagePackProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddMessagePackSerializer();
		return services.BuildServiceProvider();
	}

	private static ServiceProvider BuildDispatchProvider(IDispatcher dispatcher)
	{
		var services = new ServiceCollection();
		_ = services.AddScoped(_ => dispatcher);
		return services.BuildServiceProvider();
	}

	private static IDispatcher CreateSucceedingDispatcher()
	{
		IMessageResult success = MessageResult.Success();
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(success));
		return dispatcher;
	}

	/// <summary>
	/// Writes a message through the real <see cref="MessageInbox" /> and returns the entry the store was
	/// asked to persist.
	/// </summary>
	private static async Task<InboxEntry> WriteThroughInboxAsync(
		BinaryInboxMessage message,
		IPayloadSerializer? payloadSerializer)
	{
		InboxEntry? written = null;
		var writeStore = A.Fake<IInboxStore>();
		_ = A.CallTo(() => writeStore.CreateEntryAsync(
				A<string>._,
				A<string>._,
				A<string>._,
				A<byte[]>._,
				A<IDictionary<string, object>>._,
				A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				written = new InboxEntry
				{
					MessageId = (string)call.Arguments[0]!,
					HandlerType = HandlerTypeName,
					MessageType = (string)call.Arguments[2]!,
					Payload = (byte[])call.Arguments[3]!,
					Metadata = (IDictionary<string, object>)call.Arguments[4]!,
					RetryCount = 0,
					ReceivedAt = DateTimeOffset.UtcNow,
				};
				return new ValueTask<InboxEntry>(written);
			});

		await using (var inbox = new MessageInbox(
			writeStore,
			A.Fake<IInboxProcessor>(),
			new DispatchJsonSerializer(),
			payloadSerializer,
			Options.Create(new DeliveryInboxOptions()),
			NullLogger<MessageInbox>.Instance))
		{
			await inbox.SaveMessageAsync(
				message,
				message.Id,
				new MetadataRecord
				{
					MessageId = message.Id,
					CorrelationId = "corr-1",
					MessageType = nameof(BinaryInboxMessage),
					ContentType = "application/json",
					CreatedTimestampUtc = DateTimeOffset.UtcNow,
				},
				CancellationToken.None);
		}

		return written ?? throw new InvalidOperationException("The inbox never asked the store to create an entry.");
	}

	private static IInboxStore DrainStoreFor(InboxEntry entry)
	{
		var store = A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>());
		_ = A.CallTo(() => ((IInboxStoreAdmin)store).GetAllTenantsFailedEntriesAsync(
				A<int>._,
				A<DateTimeOffset?>._,
				A<int>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>([entry]));
		return store;
	}

	private static IOptions<DeliveryInboxOptions> SingleMessageOptions() =>
		Options.Create(new DeliveryInboxOptions
		{
			Capacity =
			{
				QueueCapacity = 1,
				ProducerBatchSize = 1,
				ConsumerBatchSize = 1,
				PerRunTotal = 1,
				ParallelProcessingDegree = 2,
			},
			MaxAttempts = 1,
			BatchTuning = { EnableBatchDatabaseOperations = false },
		});
}

/// <summary>
/// A message the JSON reader cannot parse once MessagePack has written it — the shape the drain has to
/// carry.
/// </summary>
[MessagePackObject]
public sealed class BinaryInboxMessage : IDispatchEvent
{
	/// <summary> Gets or sets the message identifier. </summary>
	[Key(0)]
	public string Id { get; set; } = string.Empty;

	/// <summary> Gets or sets the payload text asserted on after the round trip. </summary>
	[Key(1)]
	public string Text { get; set; } = string.Empty;
}
