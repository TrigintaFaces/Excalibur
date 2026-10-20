// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using MemoryPack;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Test implementation of IDomainEvent for benchmarking.
/// </summary>
[MessageName("excalibur.benchmarks.test-domain-event.v1")]
[MemoryPackable(SerializeLayout.Explicit)]
internal sealed partial class TestDomainEvent : IDomainEvent
{
	[MemoryPackOrder(0)]
	public required string EventId { get; init; }

	[MemoryPackOrder(1)]
	public required string AggregateId { get; init; }

	[MemoryPackOrder(2)]
	public required long Version { get; init; }

	[MemoryPackOrder(3)]
	public required DateTimeOffset OccurredAt { get; init; }

	[MemoryPackOrder(4)]
	public required string EventType { get; init; }

	[MemoryPackOrder(5)]
	[BenchmarkMetadataFormatter]
	public IDictionary<string, object>? Metadata { get; init; }

	[MemoryPackOrder(6)]
	public required string Data { get; init; }
}
