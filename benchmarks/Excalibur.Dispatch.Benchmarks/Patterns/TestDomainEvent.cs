// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>
/// Test implementation of IDomainEvent for benchmarking.
/// </summary>
internal sealed class TestDomainEvent : IDomainEvent
{
	public required string EventId { get; init; }

	public required string AggregateId { get; init; }

	public required long Version { get; init; }

	public required DateTimeOffset OccurredAt { get; init; }

	public required string EventType { get; init; }

	public IDictionary<string, object>? Metadata { get; init; }

	public required string Data { get; init; }
}
