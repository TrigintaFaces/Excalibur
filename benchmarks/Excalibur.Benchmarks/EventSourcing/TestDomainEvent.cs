// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// Test domain event for benchmark scenarios.
/// </summary>
public sealed class TestDomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public required string EventId { get; init; }

	/// <inheritdoc/>
	public required string AggregateId { get; init; }

	/// <inheritdoc/>
	public required long Version { get; init; }

	/// <inheritdoc/>
	public required DateTimeOffset OccurredAt { get; init; }

	/// <inheritdoc/>
	public required string EventType { get; init; }

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Test data payload.
	/// </summary>
	public string? Data { get; init; }
}
