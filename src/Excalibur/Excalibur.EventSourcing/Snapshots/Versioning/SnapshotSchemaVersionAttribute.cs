// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Snapshots.Versioning;

/// <summary>
/// Specifies the schema version of a snapshot state class.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to snapshot state classes to declare their schema version.
/// The <see cref="ISnapshotSchemaValidator"/> uses this attribute to verify that
/// snapshot data is compatible with the current code version.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SnapshotSchemaVersion(2)]
/// public class OrderSnapshot
/// {
///     public string OrderId { get; set; }
///     public string Status { get; set; }
///     public string CustomerEmail { get; set; } // Added in V2
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class SnapshotSchemaVersionAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotSchemaVersionAttribute"/> class.
	/// </summary>
	/// <param name="version">The schema version number.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 1.</exception>
	public SnapshotSchemaVersionAttribute(int version)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
		Version = version;
	}

	/// <summary>
	/// Gets the schema version number.
	/// </summary>
	/// <value>The schema version number (1-based).</value>
	public int Version { get; }
}
