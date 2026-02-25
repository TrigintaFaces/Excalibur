// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// A simple domain event for use in conformance tests.
/// </summary>
public sealed record TestDomainEvent : IDomainEvent
{
	/// <inheritdoc />
	public string EventId { get; init; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string AggregateId { get; init; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; init; }

	/// <inheritdoc />
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public string EventType => nameof(TestDomainEvent);

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Gets or sets test payload data.
	/// </summary>
	public string Payload { get; init; } = "test-payload";

	/// <summary>
	/// Creates a test event with the specified aggregate ID and version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="version">The event version.</param>
	/// <returns>A new test domain event.</returns>
	public static TestDomainEvent Create(string aggregateId, long version) =>
		new()
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = version,
			OccurredAt = DateTimeOffset.UtcNow,
			Payload = $"payload-v{version}"
		};
}
