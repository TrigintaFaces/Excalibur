// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     In-memory implementation of <see cref="ITransportTestHarness" /> used to validate shared transport behaviors.
/// </summary>
internal sealed class InMemoryTransportHarness : ITransportTestHarness
{
	private readonly Lock _sync = new();
	private readonly Queue<Envelope> _pending = new();
	private readonly Dictionary<string, Envelope> _inFlight = new(StringComparer.Ordinal);
	private readonly Queue<CloudEvent> _cloudEvents = new();
	private readonly List<TransportTestDeadLetterMessage> _deadLetters = [];
	private readonly HashSet<string> _knownMessageIds = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _messageAvailable = new(0);
	private readonly SemaphoreSlim _cloudEventAvailable = new(0);

	public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => PurgeAsync(cancellationToken);

	public ValueTask PublishAsync(TransportTestMessage message, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(message);

		lock (_sync)
		{
			if (!_knownMessageIds.Add(message.Id))
			{
				return ValueTask.CompletedTask;
			}

			_pending.Enqueue(new Envelope(message, DateTimeOffset.UtcNow));
		}

		_ = _messageAvailable.Release();
		return ValueTask.CompletedTask;
	}

	public async ValueTask PublishDuplicateAsync(
		TransportTestMessage original,
		TransportTestMessage duplicate,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(original);

		ArgumentNullException.ThrowIfNull(duplicate);

		await PublishAsync(original, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		await PublishAsync(duplicate, cancellationToken).ConfigureAwait(false);
	}

	public async ValueTask<TransportTestReceiveContext?> ReceiveAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(timeout.Ticks, 0, nameof(timeout));
		{
		}

		if (!await _messageAvailable.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		Envelope envelope;
		lock (_sync)
		{
			envelope = _pending.Dequeue();
			envelope = envelope with
			{
				DeliveryAttempt = envelope.DeliveryAttempt + 1,
				LastDeliveredAtUtc = DateTimeOffset.UtcNow,
				ReceiptToken = Guid.NewGuid().ToString("N"),
			};

			envelope.Metadata["delivery-attempt"] = envelope.DeliveryAttempt.ToString(CultureInfo.InvariantCulture);
			envelope.Metadata["enqueued-at"] = envelope.EnqueuedAtUtc.ToString("O", CultureInfo.InvariantCulture);

			_inFlight[envelope.ReceiptToken] = envelope;
		}

		return envelope.ToContext();
	}

	public ValueTask AcknowledgeAsync(TransportTestReceiveContext context, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock (_sync)
		{
			if (_inFlight.Remove(context.ReceiptToken, out var envelope))
			{
				_ = _knownMessageIds.Remove(envelope.Message.Id);
			}
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask NegativeAcknowledgeAsync(
		TransportTestReceiveContext context,
		bool requeue,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock (_sync)
		{
			if (!_inFlight.Remove(context.ReceiptToken, out var envelope))
			{
				return ValueTask.CompletedTask;
			}

			if (requeue)
			{
				_pending.Enqueue(envelope);
				_ = _messageAvailable.Release();
			}
			else
			{
				_ = _knownMessageIds.Remove(envelope.Message.Id);
				_deadLetters.Add(new TransportTestDeadLetterMessage(
					envelope.Message,
					"nack",
					"Message negatively acknowledged without requeue",
					DateTimeOffset.UtcNow,
					envelope.Metadata));
			}
		}

		return ValueTask.CompletedTask;
	}

	public async ValueTask<IReadOnlyList<TransportTestDeadLetterMessage>> ReadDeadLettersAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (true)
		{
			lock (_sync)
			{
				if (_deadLetters.Count > 0)
				{
					var snapshot = _deadLetters.ToArray();
					_deadLetters.Clear();
					return snapshot;
				}
			}

			if (DateTimeOffset.UtcNow >= deadline)
			{
				return Array.Empty<TransportTestDeadLetterMessage>();
			}

			await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
		}
	}

	public ValueTask PublishCloudEventAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		lock (_sync)
		{
			_cloudEvents.Enqueue(CloneCloudEvent(cloudEvent));
		}

		_ = _cloudEventAvailable.Release();
		return ValueTask.CompletedTask;
	}

	public async ValueTask<CloudEvent?> ReceiveCloudEventAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		if (!await _cloudEventAvailable.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		lock (_sync)
		{
			return _cloudEvents.Count > 0 ? _cloudEvents.Dequeue() : null;
		}
	}

	public ValueTask PurgeAsync(CancellationToken cancellationToken = default)
	{
		lock (_sync)
		{
			_pending.Clear();
			_inFlight.Clear();
			_deadLetters.Clear();
			_cloudEvents.Clear();
			_knownMessageIds.Clear();

			Drain(_messageAvailable);
			Drain(_cloudEventAvailable);
		}

		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_messageAvailable.Dispose();
		_cloudEventAvailable.Dispose();
		return ValueTask.CompletedTask;
	}

	private sealed record Envelope(
		TransportTestMessage Message,
		DateTimeOffset EnqueuedAtUtc,
		Dictionary<string, string> Metadata,
		int DeliveryAttempt,
		DateTimeOffset? LastDeliveredAtUtc,
		string? ReceiptToken)
	{
		public Envelope(TransportTestMessage message, DateTimeOffset enqueuedAtUtc)
			: this(message, enqueuedAtUtc, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), 0, null, null)
		{
		}

		public TransportTestReceiveContext ToContext() =>
			new(
				Message,
				ReceiptToken ?? string.Empty,
				DeliveryAttempt,
				EnqueuedAtUtc,
				LastDeliveredAtUtc,
				Metadata);
	}

	private static void Drain(SemaphoreSlim semaphore)
	{
		while (semaphore.Wait(0))
		{
		}
	}

	private static CloudEvent CloneCloudEvent(CloudEvent original)
	{
		var clone = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = original.Type,
			Source = original.Source,
			DataContentType = original.DataContentType,
			DataSchema = original.DataSchema,
			Id = original.Id,
			Subject = original.Subject,
			Time = original.Time,
			Data = original.Data,
		};

		foreach (var attribute in original.ExtensionAttributes)
		{
			clone[attribute.Name] = original[attribute.Name];
		}

		return clone;
	}
}
