// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests.Diagnostics;

internal sealed record OutboxCapabilityPolicy(string Reason, bool IsCapability = false,
    bool MirrorsInner = false, bool LegacyUnconditional = false, bool DeniedByIsolation = false);

internal sealed record OutboxCapabilityEntry(Type Type, OutboxCapabilityPolicy? Policy);

/// <summary>Test-only reflected population; explicit policy never supplies the discovery population.</summary>
internal static class OutboxCapabilityCatalog
{
    internal static IReadOnlyDictionary<Type, OutboxCapabilityPolicy> ProductionPolicy { get; } =
        new Dictionary<Type, OutboxCapabilityPolicy>
        {
            [typeof(IOutboxPayloadTransformStore)] = new("Owning payload transform: telemetry mediates; encryption hides raw transform.", true, true),
            [typeof(IOutboxStoreAdmin)] = new("Store admin capability; telemetry mediates and mirrors.", true, true),
            [typeof(IMultiTransportOutboxStore)] = new("Payload-bearing fanout store capability; explicit mediation required.", true, true),
            [typeof(IMultiTransportOutboxStoreAdmin)] = new("Payload-bearing fanout admin capability; explicit mediation required.", true, true),
            [typeof(IFencedOutboxStore)] = new("Fenced claim/read capability; telemetry mediates and mirrors.", true, true),
            [typeof(IFencedOutboxStoreDiagnostics)] = new("Payload-free fencing diagnostics; presence mirrors inner.", true, true),
            [typeof(IOutboxStoreBatch)] = new("Retain existing legacy-degradation classification and dedicated mirror control.", true, LegacyUnconditional: true),
            [typeof(IDeadLetterableOutboxStore)] = new("Retain existing documented fail-loud legacy classification.", true, LegacyUnconditional: true),
            [typeof(IBackoffSchedulableOutboxStore)] = new("Retain existing legacy backoff classification and dedicated mirror control.", true, LegacyUnconditional: true),
            [typeof(ICloudNativeOutboxStoreBatch)] = new("Existing probed batch capability: telemetry forwards; encryption deliberately denies raw payload.", true, DeniedByIsolation: true),
            [typeof(IClaimScopedOutboxStore)] = new("Claim-scoped completion; presence mirrors inner.", true, true),
            [typeof(IClaimScopedDeadLetterableOutboxStore)] = new("Claim-scoped terminal reporting; presence mirrors inner.", true, true),
            [typeof(IFencedClaimScopedOutboxStore)] = new("Combined tenure and claim authority; presence mirrors inner.", true, true),
            [typeof(IFencedDeadLetterableOutboxStore)] = new("Fenced terminal reporting; presence mirrors inner.", true, true),
            [typeof(IOutboxStoreCapabilities)] = new("Descriptor discovered from store: preserve inner's own shape.", true, true),
            [typeof(IOutboxStore)] = new("Base polling store contract, not an optional capability."),
            [typeof(ICloudNativeOutboxStore)] = new("Explicit separate cloud-native store family, not a polling IOutboxStore decorator facet."),
            [typeof(ICloudNativeOutboxStoreClaim)] = new("Explicit claim contract alongside the separate cloud-native store family; not auto-forwarded through polling decorators."),
            [typeof(IOutboxBulkCleanup)] = new("DI-resolved cleanup adapter contract; OutboxBulkCleanupAdapter delegates to store admin."),
            [typeof(IOutboxDispatcher)] = new("Dispatch service contract, not a store capability."),
            [typeof(IOutboxMessage)] = new("Message data contract, not a store capability."),
            [typeof(IOutboxMessageMapper)] = new("Transport mapping service, not a store capability."),
            [typeof(IOutboxProcessor)] = new("Processing service lifecycle, not a store capability."),
            [typeof(IOutboxPublisher)] = new("Publishing service contract, not a store capability."),
            [typeof(Excalibur.Dispatch.Outbox.IOutboxWriter)] = new("Producer writer service, not a polling store capability."),
            [typeof(ITransactionalOutboxPreparer)] = new("Selected owning preparation facet; telemetry mediates returned staging handles and encryption mediates encoded payload preparation.", true, true),
            [typeof(ITransactionalOutboxPayloadTransformPreparer)] = new("Selected payload-transform preparation facet; telemetry mediates it, while an encryption boundary withholds raw transforms.", true, true),
            [typeof(IPreparedTransactionalOutboxWrite)] = new("Returned immutable process-local transaction handle, not a capability discovered on an outbox store; staging is mediated by the selected preparer."),
            [typeof(ITransactionalOutboxWriter)] = new("Explicit raw transaction writer resolved by owning unit of work; not a payload-safe decorator forwarding permission."),
        };

    internal static IReadOnlyList<OutboxCapabilityEntry> ProductionEntries => Build(
        [typeof(IOutboxStore).Assembly, typeof(ICloudNativeOutboxStoreBatch).Assembly], ProductionPolicy);

    internal static IReadOnlyList<OutboxCapabilityEntry> Build(IEnumerable<Assembly> assemblies,
        IReadOnlyDictionary<Type, OutboxCapabilityPolicy> policy) => Classify(Discover(assemblies), policy);

    internal static IReadOnlyList<Type> Discover(IEnumerable<Assembly> assemblies)
    {
        // GetTypes propagates ReflectionTypeLoadException: partial loads never become a passing catalog.
        return assemblies.Distinct().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsVisible && type.IsInterface
                && type.Name.StartsWith('I')
                && type.Name.Contains("Outbox", StringComparison.Ordinal))
            .Distinct().OrderBy(static type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<OutboxCapabilityEntry> Classify(IEnumerable<Type> discovered,
        IReadOnlyDictionary<Type, OutboxCapabilityPolicy> policy) => discovered
        .Select(type => new OutboxCapabilityEntry(type, policy.GetValueOrDefault(type))).ToArray();

    internal static IReadOnlyList<string> Analyze(IReadOnlyList<OutboxCapabilityEntry> entries,
        Func<Type, bool> innerExposes, Func<Type, bool> decoratorExposes)
    {
        var diagnostics = new List<string>();
        if (entries.Count == 0) { diagnostics.Add("EmptyCatalog"); }
        foreach (var collision in entries.GroupBy(static entry => entry.Type.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1))
        {
            diagnostics.Add("AmbiguousShortName: " + string.Join("; ", collision.Select(static entry => Identity(entry.Type))));
        }

        foreach (var entry in entries)
        {
            if (entry.Policy is null)
            {
                diagnostics.Add("Unclassified: " + Identity(entry.Type));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Policy.Reason))
            {
                diagnostics.Add("MissingClassificationReason: " + Identity(entry.Type));
            }

            var behaviorCount = (entry.Policy.MirrorsInner ? 1 : 0)
                + (entry.Policy.LegacyUnconditional ? 1 : 0) + (entry.Policy.DeniedByIsolation ? 1 : 0);
            if (entry.Policy.IsCapability ? behaviorCount != 1 : behaviorCount != 0)
            {
                diagnostics.Add("InvalidCapabilityPolicy: " + Identity(entry.Type));
                continue;
            }

            if (!entry.Policy.IsCapability) { continue; }
            var inner = innerExposes(entry.Type);
            var decorated = decoratorExposes(entry.Type);
            if (inner && !decorated) { diagnostics.Add("DroppedCapability: " + Identity(entry.Type)); }
            if (entry.Policy.MirrorsInner && !inner && decorated)
            {
                diagnostics.Add("AdvertisedWithoutInner: " + Identity(entry.Type));
            }
        }

        return diagnostics;
    }

    private static string Identity(Type type) => type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
}
