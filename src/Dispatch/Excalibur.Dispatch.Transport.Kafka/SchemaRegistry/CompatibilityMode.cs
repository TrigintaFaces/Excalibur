// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies the schema compatibility mode for the Confluent Schema Registry.
/// </summary>
/// <remarks>
/// <para>
/// These modes match the Confluent Schema Registry compatibility levels exactly:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="None"/>: No compatibility checking</description></item>
///   <item><description><see cref="Backward"/>: New schema can read data written with the previous schema</description></item>
///   <item><description><see cref="Forward"/>: Previous schema can read data written with the new schema</description></item>
///   <item><description><see cref="Full"/>: Both backward and forward compatible</description></item>
///   <item><description>Transitive variants check against all registered schema versions</description></item>
/// </list>
/// </remarks>
public enum CompatibilityMode
{
	/// <summary>
	/// No compatibility checking is performed.
	/// </summary>
	None = 0,

	/// <summary>
	/// New schema can read data produced with the immediately previous schema version.
	/// </summary>
	Backward = 1,

	/// <summary>
	/// New schema can read data produced with all previously registered schema versions.
	/// </summary>
	BackwardTransitive = 2,

	/// <summary>
	/// Previous schema can read data produced with the new schema version.
	/// </summary>
	Forward = 3,

	/// <summary>
	/// All previously registered schemas can read data produced with the new schema.
	/// </summary>
	ForwardTransitive = 4,

	/// <summary>
	/// Both backward and forward compatible with the immediately previous schema version.
	/// </summary>
	Full = 5,

	/// <summary>
	/// Both backward and forward compatible with all previously registered schema versions.
	/// </summary>
	FullTransitive = 6
}

/// <summary>
/// Extension methods for <see cref="CompatibilityMode"/>.
/// </summary>
public static class CompatibilityModeExtensions
{
	/// <summary>
	/// Converts the compatibility mode to the Confluent Schema Registry API string value.
	/// </summary>
	/// <param name="mode">The compatibility mode.</param>
	/// <returns>The API string representation.</returns>
	public static string ToApiString(this CompatibilityMode mode) => mode switch
	{
		CompatibilityMode.None => "NONE",
		CompatibilityMode.Backward => "BACKWARD",
		CompatibilityMode.BackwardTransitive => "BACKWARD_TRANSITIVE",
		CompatibilityMode.Forward => "FORWARD",
		CompatibilityMode.ForwardTransitive => "FORWARD_TRANSITIVE",
		CompatibilityMode.Full => "FULL",
		CompatibilityMode.FullTransitive => "FULL_TRANSITIVE",
		_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown compatibility mode")
	};

	/// <summary>
	/// Parses a Confluent Schema Registry API string value to a compatibility mode.
	/// </summary>
	/// <param name="apiValue">The API string value.</param>
	/// <returns>The corresponding compatibility mode.</returns>
	/// <exception cref="ArgumentException">The API value is not recognized.</exception>
	public static CompatibilityMode ParseCompatibilityMode(string apiValue) => apiValue?.ToUpperInvariant() switch
	{
		"NONE" => CompatibilityMode.None,
		"BACKWARD" => CompatibilityMode.Backward,
		"BACKWARD_TRANSITIVE" => CompatibilityMode.BackwardTransitive,
		"FORWARD" => CompatibilityMode.Forward,
		"FORWARD_TRANSITIVE" => CompatibilityMode.ForwardTransitive,
		"FULL" => CompatibilityMode.Full,
		"FULL_TRANSITIVE" => CompatibilityMode.FullTransitive,
		_ => throw new ArgumentException($"Unknown compatibility mode: {apiValue}", nameof(apiValue))
	};
}
