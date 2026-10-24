// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Pairs a trusted source interface with its published kit; naming conventions do not infer pairings.</summary>
internal sealed record BackendContract(string Project, string MetadataName, string Kit)
{
    /// <summary>Gets the explicit contract population preserved from the prior gate.</summary>
    internal static BackendContract[] All { get; } =
    [
        new("src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj", "Excalibur.Dispatch.Messaging.ISagaStore", "SagaStoreConformanceTestKit"),
        new("src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj", "Excalibur.Dispatch.IInboxStore", "InboxStoreConformanceTestKit"),
        new("src/Excalibur/Excalibur.Compliance.Abstractions/Excalibur.Compliance.Abstractions.csproj", "Excalibur.Compliance.IAuditStore", "AuditStoreConformanceTestKit"),
        new("src/Dispatch/Excalibur.Dispatch.LeaderElection.Abstractions/Excalibur.Dispatch.LeaderElection.Abstractions.csproj", "Excalibur.Dispatch.LeaderElection.ILeaderElection", "LeaderElectionConformanceTestKit"),
        new("src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj", "Excalibur.Dispatch.IOutboxStore", "OutboxStoreConformanceTestKit"),
        new("src/Excalibur/Excalibur.EventSourcing.Abstractions/Excalibur.EventSourcing.Abstractions.csproj", "Excalibur.EventSourcing.IEventStore", "EventStoreConformanceTestKit"),
        new("src/Excalibur/Excalibur.Data.Abstractions/Excalibur.Data.Abstractions.csproj", "Excalibur.Data.CloudNative.ICloudNativeOutboxStore", "CloudNativeOutboxStoreConformanceTestKit"),
        new("src/Excalibur/Excalibur.EventSourcing.Abstractions/Excalibur.EventSourcing.Abstractions.csproj", "Excalibur.EventSourcing.ISnapshotStore", "SnapshotStoreConformanceTestKit"),
    ];
}
