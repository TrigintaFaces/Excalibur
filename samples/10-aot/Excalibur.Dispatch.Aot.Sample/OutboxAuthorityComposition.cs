// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Owns a public DI composition; all store behavior comes from the framework.</summary>
internal sealed class OutboxAuthorityComposition : IAsyncDisposable
{
    private const string MessageType = "native-outbox-authority";

    private OutboxAuthorityComposition(ServiceProvider services)
    {
        Services = services;
        Store = services.GetRequiredService<IOutboxStore>();
        OutboxAuthoritySmoke.Require(Store is InMemoryOutboxStore, "public DI must resolve the real framework store");
        FencedStore = Store.GetService(typeof(IFencedOutboxStore)) as IFencedOutboxStore
            ?? throw new InvalidOperationException("The framework store must expose its fenced capability.");
        Diagnostics = Store.GetService(typeof(IFencedOutboxStoreDiagnostics)) as IFencedOutboxStoreDiagnostics
            ?? throw new InvalidOperationException("The framework store must expose its high-water diagnostics.");
        Owner = services.GetRequiredService<OutboxAuthorityOwner>();
        Adapter = services.GetRequiredService<OutboxAuthorityRecordingAdapter>();
    }

    internal ServiceProvider Services { get; }
    internal IOutboxStore Store { get; }
    internal IFencedOutboxStore FencedStore { get; }
    internal IFencedOutboxStoreDiagnostics Diagnostics { get; }
    internal OutboxAuthorityOwner Owner { get; }
    internal OutboxAuthorityRecordingAdapter Adapter { get; }

    internal static async Task<OutboxAuthorityComposition> CreateAsync()
    {
        MessageTypeRegistry.RegisterType<OutboxAuthorityMessage>();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddInMemoryOutboxStore(); // Default capacity, lease and retention are unchanged.
        services.AddSingleton(static _ => new OutboxAuthorityOwner());
        services.AddSingleton(static _ => new OutboxAuthorityRecordingAdapter());
        services.AddSingleton<IMessageBusAdapter>(sp => sp.GetRequiredService<OutboxAuthorityRecordingAdapter>());
        services.AddScoped<ILeaderProcessingGate>(sp => new OutboxAuthorityGate(sp.GetRequiredService<OutboxAuthorityOwner>()));
        services.Configure<OutboxDeliveryOptions>(options => options.SingleActiveWriter = false);

        // Reuse the framework codec and payload framing. Every sample payload is covered by generated metadata.
        services.AddPluggableSerializer(SerializerIds.SystemTextJson,
            SystemTextJsonSerializer.Create(AppJsonSerializerContext.Default), setAsCurrent: true);
        services.AddScoped<IOutboxPublisher>(sp => new MessageBusOutboxPublisher(
            sp.GetRequiredService<IOutboxStore>(), sp.GetRequiredService<IPayloadSerializer>(),
            sp.GetRequiredService<IMessageBusAdapter>(), sp, sp.GetRequiredService<ILogger<MessageBusOutboxPublisher>>()));

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        try { return new OutboxAuthorityComposition(provider); }
        catch (Exception setupError)
        {
            try { await provider.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
            throw;
        }
    }

    internal async Task<OutboundMessage> StageAsync(string label, int priority, CancellationToken cancellationToken)
    {
        var payload = new OutboxAuthorityMessage { Label = label, Sequence = priority + 1 };
        var bytes = Services.GetRequiredService<IPayloadSerializer>().Serialize(payload);
        var row = new OutboundMessage(MessageType, bytes, "recording-only") { Priority = priority };
        await Store.StageMessageAsync(row, cancellationToken).ConfigureAwait(false);
        return row;
    }

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}

internal sealed class OutboxAuthorityOwner
{
    internal object Identity { get; } = new();
    internal long Token = 7;
}

internal sealed class OutboxAuthorityGate(OutboxAuthorityOwner owner) : ILeaderProcessingGate, ILeaderProcessingAuthorityDomain
{
    public bool ShouldProcess => true;
    public long? FencingToken => Volatile.Read(ref owner.Token);
    public object AuthorityDomain => owner.Identity;
}
