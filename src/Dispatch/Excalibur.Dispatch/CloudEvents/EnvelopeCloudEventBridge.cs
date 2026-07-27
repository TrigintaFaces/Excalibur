// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Default implementation of <see cref="IEnvelopeCloudEventBridge" /> that composes the
/// <see cref="ICloudEventEnvelopeConverter" /> with registered CloudEvent mapper adapters to translate
/// between Dispatch envelopes and transport-specific representations.
/// </summary>
/// <remarks>
/// Mappers are resolved through pre-closed <see cref="ICloudEventMapperAdapter"/> instances keyed by
/// transport-message <see cref="Type"/>. This keeps the bridge trimming/AOT-safe: there is no
/// <see cref="System.Type.MakeGenericType(System.Type[])"/> and no late-bound <c>dynamic</c> dispatch —
/// each generic mapper is closed once, at its DI registration site, via
/// <c>AddCloudEventMapper</c>.
/// </remarks>
public sealed class EnvelopeCloudEventBridge : IEnvelopeCloudEventBridge
{
	private readonly ICloudEventEnvelopeConverter _converter;
	private readonly IReadOnlyDictionary<Type, ICloudEventMapperAdapter> _adapters;

	/// <summary>
	/// Initializes a new instance of the <see cref="EnvelopeCloudEventBridge" /> class.
	/// </summary>
	/// <param name="converter"> The envelope converter that produces <see cref="CloudEvent" /> instances. </param>
	/// <param name="adapters"> The registered transport mapper adapters, keyed internally by transport-message type. </param>
	public EnvelopeCloudEventBridge(
		ICloudEventEnvelopeConverter converter,
		IEnumerable<ICloudEventMapperAdapter> adapters)
	{
		_converter = converter ?? throw new ArgumentNullException(nameof(converter));
		ArgumentNullException.ThrowIfNull(adapters);

		var map = new Dictionary<Type, ICloudEventMapperAdapter>();
		foreach (var adapter in adapters)
		{
			// Last registration wins for a given transport type (matches TryAdd-style override semantics).
			map[adapter.TransportMessageType] = adapter;
		}

		_adapters = map;
	}

	/// <inheritdoc />
	public async Task<TTransportMessage> ToTransportAsync<TTransportMessage>(
		MessageEnvelope envelope,
		CloudEventMode mode,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		var cloudEvent = await _converter.FromEnvelopeAsync(envelope, cancellationToken).ConfigureAwait(false);

		if (typeof(TTransportMessage) == typeof(CloudEvent))
		{
			return (TTransportMessage)(object)cloudEvent;
		}

		var adapter = ResolveAdapter(typeof(TTransportMessage));
		var transportMessage = await adapter.ToTransportAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
		return (TTransportMessage)transportMessage;
	}

	/// <inheritdoc />
	public async Task<MessageEnvelope> FromTransportAsync(
		object transportMessage,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);

		if (transportMessage is CloudEvent cloudEvent)
		{
			return await _converter.ToEnvelopeAsync(cloudEvent, cancellationToken).ConfigureAwait(false);
		}

		var adapter = ResolveAdapter(transportMessage.GetType());
		var cloudEventResult = await adapter.FromTransportAsync(transportMessage, cancellationToken).ConfigureAwait(false);

		return await _converter.ToEnvelopeAsync(cloudEventResult, cancellationToken).ConfigureAwait(false);
	}

	private ICloudEventMapperAdapter ResolveAdapter(Type transportMessageType) =>
		_adapters.TryGetValue(transportMessageType, out var adapter)
			? adapter
			: throw new InvalidOperationException(
				$"No CloudEvent mapper registered for transport message type '{transportMessageType.FullName}'. " +
				$"Register one with services.AddCloudEventMapper<{transportMessageType.Name}, TMapper>().");
}
