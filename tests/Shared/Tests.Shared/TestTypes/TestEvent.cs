// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.TestTypes;

/// <summary>
/// A simple test event for use in integration tests.
/// Implements IDispatchEvent for domain event testing.
/// </summary>
public class TestEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the event ID.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the aggregate ID.
	/// </summary>
	public string AggregateId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the version.
	/// </summary>
	public int Version { get; set; } = 1;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	public string EventType { get; set; } = nameof(TestEvent);

	/// <summary>
	/// Gets or sets the event metadata.
	/// </summary>
	public IReadOnlyDictionary<string, object?> Metadata { get; set; } = new Dictionary<string, object?>();

	/// <summary>
	/// Gets or sets the event data.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}
