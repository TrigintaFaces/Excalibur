// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text;

using Excalibur.Domain.Model;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// A simple snapshot implementation for use in conformance tests.
/// </summary>
public sealed class TestSnapshot : ISnapshot
{
	/// <inheritdoc />
	public string SnapshotId { get; init; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public string AggregateId { get; init; } = string.Empty;

	/// <inheritdoc />
	public long Version { get; init; }

	/// <inheritdoc />
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public byte[] Data { get; init; } = [];

	/// <inheritdoc />
	public string AggregateType { get; init; } = "TestAggregate";

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Creates a test snapshot with the specified parameters.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">The snapshot version.</param>
	/// <param name="state">Optional state data.</param>
	/// <returns>A new test snapshot.</returns>
	public static TestSnapshot Create(
		string aggregateId,
		string aggregateType,
		long version,
		string? state = null) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Version = version,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = Encoding.UTF8.GetBytes(state ?? $"state-v{version}")
		};
}
