// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Metadata;

namespace Excalibur.Saga.Orchestration;

/// <summary>Owns one explicit state codec and mediated emission preparation selection.</summary>
internal sealed class SagaAtomicPreparationPolicy(
    Func<object, Type, byte[]> encodeState,
    Func<byte[], Type, object?> decodeState,
    Func<IOutboxStore, IDispatchMessage, IMessageContext, CancellationToken,
        ValueTask<IPreparedTransactionalOutboxWrite>> prepareEmission)
{
    internal byte[] EncodeState(object state, Type stateType) => encodeState(state, stateType);

    internal object? DecodeState(byte[] state, Type stateType) => decodeState(state, stateType);

    internal ValueTask<IPreparedTransactionalOutboxWrite> PrepareEmissionAsync(
        IOutboxStore selectedOuterStore, IDispatchMessage message, IMessageContext context,
        string authoritativeTenantId, CancellationToken cancellationToken)
    {
        // Fill absence only on the detached child; preserve an explicit different tenant so
        // the unchanged strict transition-provenance guard refuses it rather than relabeling it.
        if (context.GetTenantId() is null)
        {
            context.GetOrCreateIdentityFeature().TenantId = authoritativeTenantId;
        }

        return prepareEmission(selectedOuterStore, message, context, cancellationToken);
    }

    internal static SagaAtomicPreparationPolicy CreateGenerated(
        IJsonTypeInfoResolver stateMetadata, IPayloadSerializer selectedPayloadSerializer)
    {
        ArgumentNullException.ThrowIfNull(stateMetadata);
        ArgumentNullException.ThrowIfNull(selectedPayloadSerializer);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = stateMetadata,
        };
        options.MakeReadOnly();
        var metadata = new ConcurrentDictionary<Type, JsonTypeInfo>();
        JsonTypeInfo GetMetadata(Type type) => metadata.GetOrAdd(type,
            static (stateType, owner) => RequireMetadata(owner.Options, owner.Resolver, stateType),
            (Options: options, Resolver: stateMetadata));
        return new SagaAtomicPreparationPolicy(
            (value, type) => JsonSerializer.SerializeToUtf8Bytes(value, GetMetadata(type)),
            (bytes, type) => JsonSerializer.Deserialize(bytes, GetMetadata(type)),
            (outer, message, context, token) => PrepareEncodedAsync(
                outer, selectedPayloadSerializer, message, context, token));
    }

    [RequiresUnreferencedCode("Legacy durable saga preparation uses the selected runtime serializer and reflective convenience outbox preparation. Use generated state metadata and the configured metadata-capable payload codec instead.")]
    [RequiresDynamicCode("Legacy durable saga preparation can require runtime serialization code generation. Use the explicit generated preparation route for NativeAOT.")]
    internal static SagaAtomicPreparationPolicy CreateRuntime(DispatchJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        return new SagaAtomicPreparationPolicy(
            (value, type) => Encoding.UTF8.GetBytes(serializer.Serialize(value, type)),
            (bytes, type) => serializer.DeserializeFromBytes(bytes, type),
            (outer, message, context, token) => RequirePreparer(outer).PrepareEnqueueAsync(message, context, token));
    }

    private static JsonTypeInfo RequireMetadata(JsonSerializerOptions options, IJsonTypeInfoResolver resolver, Type type)
    {
        // Options.GetTypeInfo may share a BCL cache with equivalent options whose reference
        // differs. Resolve against our exact owner explicitly, then retain the verified metadata.
        var metadata = resolver.GetTypeInfo(type, options)
            ?? throw new NotSupportedException("The selected saga state resolver has no metadata for the exact state type.");
        if (metadata.Type != type || !ReferenceEquals(metadata.Options, options))
        {
            throw new InvalidOperationException("Saga state metadata must describe the exact state type and selected read-only options.");
        }

        metadata.MakeReadOnly();
        return metadata;
    }

    private static ValueTask<IPreparedTransactionalOutboxWrite> PrepareEncodedAsync(
        IOutboxStore outer, IPayloadSerializer codec, IDispatchMessage message, IMessageContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preparer = RequirePreparer(outer);
        var runtimeType = message.GetType();
        var typeName = runtimeType.FullName ?? runtimeType.Name;
        // Preserve the selected SQL owner's runtime-type codec/framing and canonical envelope.
        // No await occurs between serialization and outer mediated preparation; that owner freezes
        // object-valued header metadata before its first encryption transform await.
        var outbound = OutboundMessage.FromContext(typeName, codec.SerializeObject(message, runtimeType),
            context.ExtractMetadata().GetDestination() ?? typeName, context);
        cancellationToken.ThrowIfCancellationRequested();
        return preparer.PrepareMessageAsync(outbound, cancellationToken);
    }

    private static ITransactionalOutboxPreparer RequirePreparer(IOutboxStore outer)
    {
        ArgumentNullException.ThrowIfNull(outer);
        return outer.GetService(typeof(ITransactionalOutboxPreparer)) as ITransactionalOutboxPreparer
            ?? throw new InvalidOperationException("The selected outer outbox store does not support mediated transactional preparation.");
    }
}
