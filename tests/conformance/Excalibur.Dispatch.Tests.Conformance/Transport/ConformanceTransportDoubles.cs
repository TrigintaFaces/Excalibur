// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// A single in-memory carrier message: the serialized body plus the transport-carrier headers and filter
/// attributes. Models what a real broker carries alongside the body so the doubles can faithfully preserve
/// (conforming) or discard (non-conforming) carrier metadata.
/// </summary>
internal sealed class CarrierMessage
{
	public string BodyJson { get; init; } = "null";
	public Dictionary<string, string> Headers { get; init; } = new(StringComparer.Ordinal);
	public Dictionary<string, string> FilterAttributes { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A <b>conforming</b> in-memory transport double: faithfully preserves carrier headers, binds CloudEvents
/// (all attributes survive), redelivers nack'd messages, and honors filtering. Capability-gated conformance
/// assertions MUST pass against this double.
/// </summary>
/// <remarks>
/// Reference behavior for the urttf7 non-vacuity gate (AC-U4): every capability assertion is GREEN here and
/// RED against <see cref="NonConformingInMemoryTransport" />.
/// </remarks>
public sealed class ConformingInMemoryTransport : ITransportConformanceCapabilities
{
	private static readonly string[] CloudEventHeaderKeys =
	{
		"ce-id", "ce-source", "ce-type", "ce-specversion", "ce-subject", "ce-time", "ce-datacontenttype"
	};

	private readonly LinkedList<CarrierMessage> _queue = new();
	private readonly object _lock = new();

	/// <inheritdoc />
	public TransportCapability Capabilities =>
		TransportCapability.HeaderSurfacing
		| TransportCapability.CloudEventsBinding
		| TransportCapability.AckNackRedelivery
		| TransportCapability.Filtering;

	/// <inheritdoc />
	public Task SendWithHeadersAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> headers,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(headers);
		Enqueue(new CarrierMessage
		{
			BodyJson = JsonSerializer.Serialize(body),
			// Conforming: the carrier preserves every header.
			Headers = new Dictionary<string, string>(headers, StringComparer.Ordinal)
		});
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ConformanceReceiveResult<T>?> ReceiveWithContextAsync<T>(CancellationToken cancellationToken)
	{
		var node = DequeueNode();
		if (node is null)
		{
			return Task.FromResult<ConformanceReceiveResult<T>?>(null);
		}

		var message = node.Value;
		var body = JsonSerializer.Deserialize<T>(message.BodyJson);

		// Conforming ack/nack: ack drops the (already-removed) message; nack re-enqueues it (redelivery).
		var result = new ConformanceReceiveResult<T>(
			body,
			new Dictionary<string, string>(message.Headers, StringComparer.Ordinal),
			acknowledge: _ => Task.CompletedTask,
			reject: ct =>
			{
				lock (_lock)
				{
					_ = _queue.AddFirst(message);
				}

				return Task.CompletedTask;
			});

		return Task.FromResult<ConformanceReceiveResult<T>?>(result);
	}

	/// <inheritdoc />
	public Task SendCloudEventAsync(CloudEvent cloudEvent, CloudEventBinding binding, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		// Conforming binding: map every required + optional CloudEvents attribute onto the carrier so a
		// round-trip preserves semantic equality (both structured and binary modes preserve fully here).
		var headers = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["ce-id"] = cloudEvent.Id ?? string.Empty,
			["ce-source"] = cloudEvent.Source?.ToString() ?? string.Empty,
			["ce-type"] = cloudEvent.Type ?? string.Empty,
			["ce-specversion"] = cloudEvent.SpecVersion.VersionId,
		};

		if (cloudEvent.Subject is not null) { headers["ce-subject"] = cloudEvent.Subject; }
		if (cloudEvent.Time is { } time) { headers["ce-time"] = time.ToString("O"); }
		if (cloudEvent.DataContentType is not null) { headers["ce-datacontenttype"] = cloudEvent.DataContentType; }

		Enqueue(new CarrierMessage
		{
			BodyJson = JsonSerializer.Serialize(cloudEvent.Data),
			Headers = headers
		});
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<CloudEvent?> ReceiveCloudEventAsync(CloudEventBinding binding, CancellationToken cancellationToken)
	{
		var node = DequeueNode();
		if (node is null)
		{
			return Task.FromResult<CloudEvent?>(null);
		}

		var message = node.Value;

		// A real CloudEvents binding only yields a CloudEvent when CE attributes are present on the carrier.
		if (!message.Headers.ContainsKey("ce-id"))
		{
			return Task.FromResult<CloudEvent?>(null);
		}

		var cloudEvent = new CloudEvent
		{
			Id = message.Headers.GetValueOrDefault("ce-id"),
			Source = TryUri(message.Headers.GetValueOrDefault("ce-source")),
			Type = message.Headers.GetValueOrDefault("ce-type"),
			Subject = message.Headers.GetValueOrDefault("ce-subject"),
			Time = message.Headers.TryGetValue("ce-time", out var t)
				? DateTimeOffset.Parse(t, System.Globalization.CultureInfo.InvariantCulture)
				: null,
			DataContentType = message.Headers.GetValueOrDefault("ce-datacontenttype"),
			Data = message.BodyJson
		};

		return Task.FromResult<CloudEvent?>(cloudEvent);
	}

	/// <inheritdoc />
	public Task SendFilterableAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> attributes,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		Enqueue(new CarrierMessage
		{
			BodyJson = JsonSerializer.Serialize(body),
			FilterAttributes = new Dictionary<string, string>(attributes, StringComparer.Ordinal)
		});
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ConformanceReceiveResult<T>?> ReceiveMatchingAsync<T>(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		lock (_lock)
		{
			// Conforming filtering: skip (drop) any message whose attributes do not match the filter,
			// returning only a matching one.
			for (var node = _queue.First; node is not null; node = node.Next)
			{
				if (Matches(node.Value.FilterAttributes, filter))
				{
					_queue.Remove(node);
					var body = JsonSerializer.Deserialize<T>(node.Value.BodyJson);
					var result = new ConformanceReceiveResult<T>(
						body,
						new Dictionary<string, string>(node.Value.Headers, StringComparer.Ordinal),
						acknowledge: _ => Task.CompletedTask,
						reject: _ => Task.CompletedTask);
					return Task.FromResult<ConformanceReceiveResult<T>?>(result);
				}
			}
		}

		return Task.FromResult<ConformanceReceiveResult<T>?>(null);
	}

	private static bool Matches(
		IReadOnlyDictionary<string, string> attributes,
		IReadOnlyDictionary<string, string> filter)
	{
		foreach (var (key, value) in filter)
		{
			if (!attributes.TryGetValue(key, out var actual) || !string.Equals(actual, value, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	private static Uri? TryUri(string? value) =>
		Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri) ? uri : null;

	private void Enqueue(CarrierMessage message)
	{
		lock (_lock)
		{
			_ = _queue.AddLast(message);
		}
	}

	private LinkedListNode<CarrierMessage>? DequeueNode()
	{
		lock (_lock)
		{
			var node = _queue.First;
			if (node is null)
			{
				return null;
			}

			_queue.RemoveFirst();
			return node;
		}
	}
}

/// <summary>
/// A <b>non-conforming</b> in-memory transport double that <i>advertises</i> all capabilities but implements
/// them incorrectly: it discards carrier headers, yields no CloudEvent (zero CE binding), never redelivers a
/// nack'd message, and ignores filtering. Every capability-gated conformance assertion MUST go RED against
/// this double — the non-vacuity gate (AC-U4) the htcbgu false-conformance defect requires.
/// </summary>
public sealed class NonConformingInMemoryTransport : ITransportConformanceCapabilities
{
	private readonly Queue<string> _bodies = new();
	private readonly object _lock = new();

	/// <inheritdoc />
	public TransportCapability Capabilities =>
		TransportCapability.HeaderSurfacing
		| TransportCapability.CloudEventsBinding
		| TransportCapability.AckNackRedelivery
		| TransportCapability.Filtering;

	/// <inheritdoc />
	public Task SendWithHeadersAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> headers,
		CancellationToken cancellationToken)
	{
		// Non-conforming: headers are DISCARDED — only the body survives.
		Enqueue(JsonSerializer.Serialize(body));
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ConformanceReceiveResult<T>?> ReceiveWithContextAsync<T>(CancellationToken cancellationToken)
	{
		if (!TryDequeue(out var bodyJson))
		{
			return Task.FromResult<ConformanceReceiveResult<T>?>(null);
		}

		var body = JsonSerializer.Deserialize<T>(bodyJson);

		// Non-conforming: no surfaced headers, and nack does NOT redeliver (message already removed).
		var result = new ConformanceReceiveResult<T>(
			body,
			headers: null,
			acknowledge: _ => Task.CompletedTask,
			reject: _ => Task.CompletedTask);

		return Task.FromResult<ConformanceReceiveResult<T>?>(result);
	}

	/// <inheritdoc />
	public Task SendCloudEventAsync(CloudEvent cloudEvent, CloudEventBinding binding, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		// Non-conforming: CE attributes are dropped — only the data body survives, as a plain message.
		Enqueue(JsonSerializer.Serialize(cloudEvent.Data));
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<CloudEvent?> ReceiveCloudEventAsync(CloudEventBinding binding, CancellationToken cancellationToken)
	{
		// Non-conforming: no CloudEvents binding — cannot reconstruct a CloudEvent.
		_ = TryDequeue(out _);
		return Task.FromResult<CloudEvent?>(null);
	}

	/// <inheritdoc />
	public Task SendFilterableAsync<T>(
		T body,
		IReadOnlyDictionary<string, string> attributes,
		CancellationToken cancellationToken)
	{
		// Non-conforming: filter attributes are dropped.
		Enqueue(JsonSerializer.Serialize(body));
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ConformanceReceiveResult<T>?> ReceiveMatchingAsync<T>(
		IReadOnlyDictionary<string, string> filter,
		CancellationToken cancellationToken)
	{
		// Non-conforming: ignores the filter — returns the next message regardless of whether it matches.
		if (!TryDequeue(out var bodyJson))
		{
			return Task.FromResult<ConformanceReceiveResult<T>?>(null);
		}

		var body = JsonSerializer.Deserialize<T>(bodyJson);
		var result = new ConformanceReceiveResult<T>(body, headers: null, acknowledge: null, reject: null);
		return Task.FromResult<ConformanceReceiveResult<T>?>(result);
	}

	private void Enqueue(string bodyJson)
	{
		lock (_lock)
		{
			_bodies.Enqueue(bodyJson);
		}
	}

	private bool TryDequeue(out string bodyJson)
	{
		lock (_lock)
		{
			return _bodies.TryDequeue(out bodyJson!);
		}
	}
}
