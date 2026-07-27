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
	public ReadOnlyMemory<byte> Data { get; init; }

	/// <inheritdoc />
	public string AggregateType { get; init; } = "TestAggregate";

	/// <inheritdoc />
	public IDictionary<string, object>? Metadata { get; init; }

	/// <inheritdoc />
	/// <remarks>
	/// Settable so a conformance arm can construct two snapshots that share an aggregate identifier and
	/// differ only by tenant. Without that, no conformance test can express the case where one tenant's
	/// snapshot is served to another — which is why the isolation gap went undetected across every
	/// provider inheriting this fixture.
	/// </remarks>
	public string? TenantId { get; init; }

	/// <summary>
	/// Creates a test snapshot with the specified parameters.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">The snapshot version.</param>
	/// <param name="state">Optional state data.</param>
	/// <returns>A new test snapshot.</returns>
	/// <remarks>
	/// Deliberately NOT given a <c>tenantId</c> parameter. Adding an optional parameter to a method
	/// already in <c>PublicAPI.Shipped.txt</c> removes the shipped signature and raises RS0017 — a
	/// binary-breaking change to a published package, for a convenience this fixture does not need.
	/// A caller that requires a tenant uses the object initializer, which is a pure addition.
	/// </remarks>
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
