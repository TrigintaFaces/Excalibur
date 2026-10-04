// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Serialization;

using Excalibur.Outbox;
using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Primary implementation of the outbox pattern for reliable message delivery with transactional guarantees. This component manages
/// deferred message publishing to ensure consistency between business operations and message delivery, implementing at-least-once delivery
/// semantics with configurable retry logic and error handling.
/// </summary>
public sealed partial class MessageOutbox : IOutboxDispatcher, IDisposable
{
	private readonly IOutboxStore _outboxStore;
	private readonly IOutboxProcessor? _legacyProcessor;
	private readonly DispatchJsonSerializer _serializer;
	private readonly ILogger<MessageOutbox> _logger;
	private readonly IServiceScopeFactory? _scopeFactory;
	private readonly OutboxDeliveryOptions _options;
	private readonly Lock _lifecycleLock = new();
	private readonly CancellationTokenSource _lifetimeCancellation = new();
	private readonly HashSet<TaskCompletionSource> _cycles = [];
	private TaskCompletionSource? _disposal;

	/// <summary>
	/// The declared message types indexed by every stored-name form a row may carry.
	/// </summary>
	/// <remarks>
	/// Built once at construction and never mutated, so concurrent resolutions read it without
	/// synchronisation. Deferring it to first use would need a barrier to be safe under a weak memory
	/// model, which is more machinery than a dictionary of the host's own declared types is worth.
	/// </remarks>
	private readonly Dictionary<string, Type> _declaredMessageTypes;

	/// <summary>Creates a dispatcher using the supplied processor. Sequential cycles must use the same processor identity.</summary>
	/// <param name="outboxStore">The message store.</param>
	/// <param name="outboxProcessor">The processor owned by this dispatcher.</param>
	/// <param name="serializer">The message serializer.</param>
	/// <param name="options">The delivery options.</param>
	/// <param name="logger">The dispatcher logger.</param>
	public MessageOutbox(IOutboxStore outboxStore, IOutboxProcessor outboxProcessor,
		DispatchJsonSerializer serializer, IOptions<OutboxDeliveryOptions> options, ILogger<MessageOutbox> logger)
		: this(outboxStore, serializer, options, logger)
	{
		_legacyProcessor = outboxProcessor ?? throw new ArgumentNullException(nameof(outboxProcessor));
	}

	/// <summary>Creates a dispatcher with an independent processor scope for each dispatch cycle.</summary>
	/// <param name="outboxStore">The message store.</param>
	/// <param name="serializer">The message serializer.</param>
	/// <param name="options">The delivery options.</param>
	/// <param name="logger">The dispatcher logger.</param>
	/// <param name="scopeFactory">The factory owning each cycle's processor lifetime.</param>
	[ActivatorUtilitiesConstructor]
	public MessageOutbox(IOutboxStore outboxStore, DispatchJsonSerializer serializer,
		IOptions<OutboxDeliveryOptions> options, ILogger<MessageOutbox> logger, IServiceScopeFactory scopeFactory)
		: this(outboxStore, serializer, options, logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
	}

	private MessageOutbox(IOutboxStore outboxStore, DispatchJsonSerializer serializer,
		IOptions<OutboxDeliveryOptions> options, ILogger<MessageOutbox> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options.Value;
		_declaredMessageTypes = BuildDeclaredMessageTypeLookup(_options.MessageTypes);
	}

	/// <summary>Runs one outbox dispatch cycle. The host or scheduler owns repetition.</summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance, used for message ownership and coordination. </param>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task containing the total number of messages processed during the dispatch session. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when dispatcherId is null or empty. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the outbox processor cannot be initialized. </exception>
	[RequiresUnreferencedCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Outbox stores serialize the message payload reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);
		cancellationToken.ThrowIfCancellationRequested();
		TaskCompletionSource completion;
		CancellationTokenSource cycleCancellation;
		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposal is not null, this);
			cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
			completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_cycles.Add(completion);
		}
		try
		{
			LogOutboxStarted();
			int processed;
			if (_scopeFactory is null)
			{
				_legacyProcessor!.Init(dispatcherId);
				processed = await _legacyProcessor.DispatchPendingMessagesAsync(cycleCancellation.Token).ConfigureAwait(false);
			}
			else
			{
				await using var scope = _scopeFactory.CreateAsyncScope();
				var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
				processor.Init(dispatcherId);
				processed = await processor.DispatchPendingMessagesAsync(cycleCancellation.Token).ConfigureAwait(false);
			}
			cycleCancellation.Token.ThrowIfCancellationRequested();
			return processed;
		}
		catch (Exception ex)
		{
			LogOutboxError(ex);
			throw;
		}
		finally
		{
			try
			{
				try
				{
					LogOutboxStopped();
				}
				finally
				{
					cycleCancellation.Dispose();
				}
			}
			finally
			{
				lock (_lifecycleLock)
				{
					_cycles.Remove(completion);
					completion.SetResult();
				}
			}
		}
	}

	/// <summary>Retained for compatibility. This method does not schedule a dispatch cycle; the host owns scheduling.</summary>
	public void SignalNewMessage()
	{
	}

	/// <summary>
	/// Saves integration events to the outbox for deferred publishing with transactional guarantees. This method serializes events and
	/// metadata, applies TTL policies, and stores them for later processing by the outbox dispatcher, ensuring reliable message delivery
	/// with at-least-once semantics.
	/// </summary>
	/// <param name="integrationEvents"> Collection of integration events to save for deferred publishing. </param>
	/// <param name="metadata"> Message metadata containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and shutdown support. </param>
	/// <returns> Task representing the asynchronous event saving operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when integrationEvents is null. </exception>
	/// <exception cref="Excalibur.Dispatch.Serialization.SerializationException"> Thrown when event or metadata serialization fails. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox serialization uses runtime type metadata for dispatch payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox serialization relies on runtime type discovery.")]
	public async Task SaveEventsAsync(
		IReadOnlyCollection<IIntegrationEvent> integrationEvents,
		IMessageMetadata metadata,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(integrationEvents);

		if (integrationEvents.Count == 0)
		{
			return;
		}

		var created = DateTimeOffset.UtcNow;
		var expires = _options.DefaultMessageTimeToLive.HasValue
			? created.Add(_options.DefaultMessageTimeToLive.Value)
			: null as DateTimeOffset?;

		var outboxMessages = integrationEvents.Select(evt => new OutboxMessage(
			messageId: Uuid7Extensions.GenerateString(),
			messageType: evt.GetType().FullName ?? evt.GetType().Name,
			messageMetadata: _serializer.Serialize(metadata),
			messageBody: _serializer.SerializeToUtf8Bytes(evt, evt.GetType()),
			createdAt: created,
			expiresAt: expires)).ToArray();

		foreach (var outboxMessage in outboxMessages)
		{
			var outboundMessage = ConvertToOutboundMessage(outboxMessage);
			await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
		}

		// Signal the dispatch loop that new messages are available
		SignalNewMessage();
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox serialization uses runtime type metadata for dispatch payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox serialization relies on runtime type discovery.")]
	public async Task<int> SaveMessagesAsync(ICollection<IOutboxMessage> outboxMessages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(outboxMessages);

		if (outboxMessages.Count == 0)
		{
			LogNoMessagesToSave();
			return 0;
		}

		var now = DateTimeOffset.UtcNow;
		var ttl = _options.DefaultMessageTimeToLive;

		foreach (var message in outboxMessages.Where(m => m.ExpiresAt is null && ttl.HasValue))
		{
			message.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl!.Value);
		}

		var count = 0;
		foreach (var message in outboxMessages)
		{
			var outboundMessage = ConvertIOutboxMessageToOutboundMessage(message);
			await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
			count++;
		}

		// Signal the dispatch loop that new messages are available
		if (count > 0)
		{
			SignalNewMessage();
		}

		return count;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox message retrieval uses runtime deserialization to restore message payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox message retrieval uses Type.GetType and JSON deserialization.")]
	public async Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
	{
		// Get pending messages from the outbox store
		// Unfenced drain: MessageOutbox holds no leadership tenure, so it claims through the unfenced
		// IOutboxStore members. The fenced drain is OutboxProcessor, which presents its leadership token.
		var pendingMessages = await _outboxStore.GetUnsentMessagesAsync(
			_options.ProducerBatchSize,
			cancellationToken).ConfigureAwait(false);

		// Convert IOutboxMessage to IDispatchMessage
		var dispatchMessages = new List<IDispatchMessage>();

		foreach (var message in pendingMessages)
		{
			try
			{
				if (ResolveMessageType(message.MessageType) is { } messageType)
				{
					// Deserialize directly from the stored UTF-8 payload bytes. Routing through
					// Encoding.UTF8.GetString first is a lossy round-trip for any payload byte that is not
					// valid UTF-8 (invalid sequences become the replacement character), corrupting binary or
					// non-UTF8 bodies; the byte-native path is the exact inverse of SerializeToUtf8Bytes.
					var deserializedMessage = _serializer.DeserializeFromBytes(message.Payload, messageType);
					if (deserializedMessage is IDispatchMessage dispatchMessage)
					{
						dispatchMessages.Add(dispatchMessage);
					}
				}
				else
				{
					LogCouldNotResolveMessageType(message.MessageType, message.Id);
				}
			}
			catch (Exception ex)
			{
				LogFailedToDeserializeMessage(message.Id, message.MessageType, ex);
			}
		}

		return dispatchMessages;
	}

	/// <summary>
	/// Resolves a stored message-type name to a type this host declared it stages.
	/// </summary>
	/// <param name="typeName">The <c>MessageType</c> value read back from the outbox row.</param>
	/// <returns>The declared type, or <see langword="null"/> when the name was not declared.</returns>
	/// <remarks>
	/// <para>
	/// <c>MessageType</c> is data read back out of the outbox table, so whatever this method consults is
	/// the set an attacker who can write that table gets to choose from. Searching the loaded assemblies
	/// made that set "every type in the process", and the deserializer runs a type's constructors and
	/// property setters as it materialises it — so a stored string reached executable code by naming it.
	/// Filtering the search by <c>IDispatchMessage</c> shrank the set without bounding it, because what
	/// implements that interface in a given process is not something this host decided.
	/// </para>
	/// <para>
	/// Resolution now consults only <see cref="OutboxDeliveryOptions.MessageTypes"/>, which the host fixes
	/// at composition. The stored name selects from that list or it does not resolve; it cannot introduce
	/// a candidate. That is the difference between a narrower search and a bounded one, and only the
	/// second is a property the deployment can rely on.
	/// </para>
	/// <para>
	/// An undeclared name takes the same path an unresolvable one always took: it is logged and the row is
	/// left staged, not marked sent, so nothing is dropped. This is also not the delivery path —
	/// <see cref="GetPendingMessagesAsync"/> is an inspection API, and the drain runs through
	/// <c>IOutboxProcessor</c> over the stored payload without materialising the message type at all — so
	/// declaring nothing costs visibility here and not delivery.
	/// </para>
	/// </remarks>
	private Type? ResolveMessageType(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		if (_declaredMessageTypes.TryGetValue(typeName, out var resolved))
		{
			return resolved;
		}

		// The write path stores Type.FullName, but a row staged by another writer may carry an
		// assembly-qualified name. Comparing the portion before the assembly lets those match a declared
		// type without widening the set the comparison draws from.
		var separatorIndex = typeName.IndexOf(',', StringComparison.Ordinal);

		return separatorIndex > 0
			&& _declaredMessageTypes.TryGetValue(typeName[..separatorIndex].Trim(), out resolved)
				? resolved
				: null;
	}

	/// <summary>
	/// Indexes the declared message types by every name form a stored row may carry.
	/// </summary>
	/// <param name="messageTypes">The types the host declared.</param>
	/// <returns>A lookup from stored name to declared type.</returns>
	/// <remarks>
	/// Types that are not <see cref="IDispatchMessage"/> are dropped rather than indexed. The list is a
	/// declaration of intent, and a host that names something it cannot dispatch has made a mistake — one
	/// that must not become a way to reintroduce an arbitrary type into the resolvable set through
	/// configuration.
	/// </remarks>
	private static Dictionary<string, Type> BuildDeclaredMessageTypeLookup(IList<Type> messageTypes)
	{
		var lookup = new Dictionary<string, Type>(StringComparer.Ordinal);

		foreach (var messageType in messageTypes)
		{
			if (messageType is null || !typeof(IDispatchMessage).IsAssignableFrom(messageType))
			{
				continue;
			}

			if (messageType.FullName is { } fullName)
			{
				lookup[fullName] = messageType;
				lookup[$"{fullName}, {messageType.Assembly.GetName().Name}"] = messageType;
			}

			if (messageType.AssemblyQualifiedName is { } qualifiedName)
			{
				lookup[qualifiedName] = messageType;
			}
		}

		return lookup;
	}

	/// <summary>
	/// Performs asynchronous cleanup of outbox resources including processor disposal and connection cleanup. This method ensures proper
	/// resource cleanup and graceful shutdown of the outbox processing infrastructure.
	/// </summary>
	/// <returns> ValueTask representing the asynchronous disposal operation. </returns>
	public async ValueTask DisposeAsync()
	{
		TaskCompletionSource completion;
		Task[] cycles;
		bool ownsDisposal;
		lock (_lifecycleLock)
		{
			ownsDisposal = _disposal is null;
			_disposal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			completion = _disposal;
			cycles = ownsDisposal ? _cycles.Select(c => c.Task).ToArray() : [];
		}
		if (ownsDisposal)
		{
			try
			{
				try
				{
					await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
				}
				finally
				{
					await Task.WhenAll(cycles).ConfigureAwait(false);
					try
					{
						if (_legacyProcessor is not null)
						{
							await _legacyProcessor.DisposeAsync().ConfigureAwait(false);
						}
					}
					finally
					{
						_lifetimeCancellation.Dispose();
					}
				}
				completion.SetResult();
			}
			catch (Exception failure)
			{
				completion.SetException(failure);
			}
		}
		await completion.Task.ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>Disposes an idle dispatcher synchronously. Active cycles require asynchronous disposal.</summary>
	public void Dispose()
	{
		TaskCompletionSource completion;
		lock (_lifecycleLock)
		{
			if (_cycles.Count != 0 || _disposal is { Task.IsCompleted: false })
			{
				throw new InvalidOperationException("Use DisposeAsync to cancel and join active outbox cycles.");
			}
			if (_disposal is not null)
			{
				return;
			}
			if (_legacyProcessor is not null and not IDisposable)
			{
				throw new InvalidOperationException("The supplied processor requires DisposeAsync.");
			}
			completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_disposal = completion;
		}
		try
		{
			try
			{
				if (_legacyProcessor is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
			finally
			{
				_lifetimeCancellation.Dispose();
			}
			completion.SetResult();
		}
		catch (Exception failure)
		{
			completion.SetException(failure);
			throw;
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Converts an OutboxMessage to OutboundMessage format.
	/// </summary>
	/// <param name="outboxMessage"> The outbox message to convert. </param>
	/// <returns> An OutboundMessage instance. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static OutboundMessage ConvertToOutboundMessage(OutboxMessage outboxMessage)
	{
		var headers = string.IsNullOrEmpty(outboxMessage.MessageMetadata)
			? []
			: JsonSerializer.Deserialize<Dictionary<string, object>>(outboxMessage.MessageMetadata, EventSerializationDefaults.Canonical) ?? [];

		return new OutboundMessage(
			messageType: outboxMessage.MessageType,
			payload: outboxMessage.MessageBody,
			destination: outboxMessage.Destination ?? "default",
			headers: headers)
		{
			Id = outboxMessage.MessageId,
			CreatedAt = outboxMessage.CreatedAt,
			ScheduledAt = outboxMessage.ScheduledAt,
			RetryCount = outboxMessage.Attempts,
			Status = OutboxStatus.Staged,
			TenantId = outboxMessage.TenantId,
			CorrelationId = outboxMessage.CorrelationId,
			CausationId = outboxMessage.CausationId,
			PartitionKey = outboxMessage.PartitionKey,
			GroupKey = outboxMessage.GroupKey,
			SequenceNumber = outboxMessage.SequenceNumber,
			TargetTransports = outboxMessage.TargetTransports,
			IsMultiTransport = outboxMessage.IsMultiTransport,
		};
	}

	/// <summary>
	/// Converts an IOutboxMessage to OutboundMessage format.
	/// </summary>
	/// <param name="outboxMessage"> The outbox message to convert. </param>
	/// <returns> An OutboundMessage instance. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static OutboundMessage ConvertIOutboxMessageToOutboundMessage(IOutboxMessage outboxMessage)
	{
		// The concrete OutboxMessage carries the dedicated routing/correlation fields; route through the
		// concrete converter so no field is dropped on the SaveMessagesAsync path.
		if (outboxMessage is OutboxMessage concrete)
		{
			return ConvertToOutboundMessage(concrete);
		}

		var headers = string.IsNullOrEmpty(outboxMessage.MessageMetadata)
			? []
			: JsonSerializer.Deserialize<Dictionary<string, object>>(outboxMessage.MessageMetadata, EventSerializationDefaults.Canonical) ?? [];

		return new OutboundMessage(
			messageType: outboxMessage.MessageType,
			payload: outboxMessage.MessageBody,
			destination: outboxMessage.Destination ?? "default",
			headers: headers)
		{
			Id = outboxMessage.MessageId,
			CreatedAt = outboxMessage.CreatedAt,
			ScheduledAt = outboxMessage.ScheduledAt,
			RetryCount = outboxMessage.Attempts,
			Status = OutboxStatus.Staged,
			TenantId = outboxMessage.TenantId,
		};
	}

	// Source-generated logging methods
	[LoggerMessage(OutboxEventId.MessageOutboxStarted, LogLevel.Information, "Outbox started.")]
	private partial void LogOutboxStarted();

	[LoggerMessage(OutboxEventId.MessageOutboxError, LogLevel.Error, "Error while processing Outbox.")]
	private partial void LogOutboxError(Exception ex);

	[LoggerMessage(OutboxEventId.MessageOutboxStopped, LogLevel.Information, "Outbox stopped.")]
	private partial void LogOutboxStopped();

	[LoggerMessage(OutboxEventId.NoMessagesToSave, LogLevel.Debug, "No messages to save to the outbox.")]
	private partial void LogNoMessagesToSave();

	[LoggerMessage(OutboxEventId.CouldNotResolveMessageType, LogLevel.Warning,
		"Could not resolve message type {MessageType} for message {MessageId}")]
	private partial void LogCouldNotResolveMessageType(string messageType, string messageId);

	[LoggerMessage(OutboxEventId.FailedToDeserializeMessage, LogLevel.Warning,
		"Failed to deserialize message {MessageId} of type {MessageType}")]
	private partial void LogFailedToDeserializeMessage(string messageId, string messageType, Exception ex);
}
