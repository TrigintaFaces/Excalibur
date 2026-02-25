// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using CloudNative.CloudEvents;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Default implementation of <see cref="IEnvelopeCloudEventBridge" /> that composes the <see cref="ICloudEventEnvelopeConverter" /> with
/// registered CloudEvent mapper instances to translate between Dispatch envelopes and transport specific representations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EnvelopeCloudEventBridge" /> class.
/// </remarks>
/// <param name="converter"> The envelope converter that produces <see cref="CloudEvent" /> instances. </param>
/// <param name="serviceProvider"> The service provider used to resolve transport specific mappers. </param>
public sealed class EnvelopeCloudEventBridge(ICloudEventEnvelopeConverter converter, IServiceProvider serviceProvider)
	: IEnvelopeCloudEventBridge
{
	private static readonly Type MapperOpenGenericType = TypeResolution.TypeResolver.ResolveType(
															 "Excalibur.Dispatch.Transport.ICloudEventMapper`1, Excalibur.Dispatch.Transport.Abstractions")
														 ?? throw new InvalidOperationException(
															 "Unable to locate Excalibur.Dispatch.Transport CloudEvent mapper type.");

	private readonly ICloudEventEnvelopeConverter _converter = converter ?? throw new ArgumentNullException(nameof(converter));
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ConcurrentDictionary<Type, Lazy<object>> _mapperCache = new();

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "CloudEvent mapping uses known transport types registered at startup.")]
	[UnconditionalSuppressMessage(
			"AotAnalysis",
			"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
			Justification = "CloudEvent mapping relies on dynamic dispatch and is not supported in AOT scenarios.")]
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

		var mapper = ResolveMapper(typeof(TTransportMessage));
		return await InvokeToTransportAsync<TTransportMessage>(mapper, cloudEvent, mode, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
			Justification = "CloudEvent mapping uses known transport types registered at startup.")]
	[UnconditionalSuppressMessage(
			"AotAnalysis",
			"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
			Justification = "CloudEvent mapping relies on dynamic dispatch and is not supported in AOT scenarios.")]
	public async Task<MessageEnvelope> FromTransportAsync(
			object transportMessage,
			CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transportMessage);

		if (transportMessage is CloudEvent cloudEvent)
		{
			return await _converter.ToEnvelopeAsync(cloudEvent, cancellationToken).ConfigureAwait(false);
		}

		var mapper = ResolveMapper(transportMessage.GetType());
		var cloudEventResult = await InvokeFromTransportAsync(mapper, transportMessage, cancellationToken)
			.ConfigureAwait(false);

		return await _converter.ToEnvelopeAsync(cloudEventResult, cancellationToken).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Uses dynamic dispatch to invoke transport mappers.")]
	[RequiresDynamicCode("Uses dynamic dispatch to invoke transport mappers.")]
	private static async Task<TTransportMessage> InvokeToTransportAsync<TTransportMessage>(
			object mapper,
			CloudEvent cloudEvent,
			CloudEventMode mode,
			CancellationToken cancellationToken)
	{
		dynamic dynamicMapper = mapper;
		return await dynamicMapper.ToTransportMessageAsync(cloudEvent, mode, cancellationToken).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Uses dynamic dispatch to invoke transport mappers.")]
	[RequiresDynamicCode("Uses dynamic dispatch to invoke transport mappers.")]
	private static async Task<CloudEvent> InvokeFromTransportAsync(
			object mapper,
			object transportMessage,
			CancellationToken cancellationToken)
	{
		dynamic dynamicMapper = mapper;
		return await dynamicMapper.FromTransportMessageAsync((dynamic)transportMessage, cancellationToken).ConfigureAwait(false);
	}

	[RequiresUnreferencedCode("Uses dynamic mapper resolution for transport message types.")]
	[RequiresDynamicCode("Uses dynamic mapper resolution for transport message types.")]
	private object ResolveMapper(Type transportMessageType) => _mapperCache.GetOrAdd(
			transportMessageType,
			(key, self) => new Lazy<object>(() => self.ResolveMapperInstance(key)),
			this).Value;

	[RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
	private object ResolveMapperInstance(Type transportMessageType)
	{
		var mapperType = MapperOpenGenericType.MakeGenericType(transportMessageType);
		var mapper = _serviceProvider.GetService(mapperType) ?? throw new InvalidOperationException(
				$"No CloudEvent mapper registered for transport message type '{transportMessageType.FullName}'.");

		return mapper;
	}
}
