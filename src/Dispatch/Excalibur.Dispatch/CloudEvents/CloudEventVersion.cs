// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Provides versioning support for CloudEvents to handle schema evolution.
/// </summary>
public static class CloudEventVersion
{
	/// <summary>
	/// The current CloudEvents specification version supported.
	/// </summary>
	public const string SpecVersion = "1.0";

	/// <summary>
	/// Extension attribute name for schema version.
	/// </summary>
	public const string SchemaVersionAttribute = "schemaversion";

	/// <summary>
	/// Extension attribute name for schema compatibility mode.
	/// </summary>
	public const string SchemaCompatibilityAttribute = "schemacompatibility";

	/// <summary>
	/// Gets the schema version from a CloudEvent.
	/// </summary>
	public static string? GetSchemaVersion(this CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		return cloudEvent[SchemaVersionAttribute] as string;
	}

	/// <summary>
	/// Sets the schema version on a CloudEvent.
	/// </summary>
	public static void SetSchemaVersion(this CloudEvent cloudEvent, string version)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		ArgumentException.ThrowIfNullOrWhiteSpace(version);

		cloudEvent[SchemaVersionAttribute] = version;
	}

	/// <summary>
	/// Gets the schema compatibility mode from a CloudEvent.
	/// </summary>
	public static SchemaCompatibilityMode GetSchemaCompatibility(this CloudEvent cloudEvent)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		var value = cloudEvent[SchemaCompatibilityAttribute] as string;

		return value?.ToUpperInvariant() switch
		{
			"NONE" => SchemaCompatibilityMode.None,
			"FORWARD" => SchemaCompatibilityMode.Forward,
			"BACKWARD" => SchemaCompatibilityMode.Backward,
			"FULL" => SchemaCompatibilityMode.Full,
			_ => SchemaCompatibilityMode.None,
		};
	}

	/// <summary>
	/// Sets the schema compatibility mode on a CloudEvent.
	/// </summary>
	public static void SetSchemaCompatibility(this CloudEvent cloudEvent, SchemaCompatibilityMode mode)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		cloudEvent[SchemaCompatibilityAttribute] = mode.ToString().ToUpperInvariant();
	}

	/// <summary>
	/// Creates a version-aware CloudEvent.
	/// </summary>
	public static CloudEvent CreateVersioned(
		string source,
		string type,
		object data,
		string schemaVersion,
		SchemaCompatibilityMode compatibility = SchemaCompatibilityMode.None)
	{
		var cloudEvent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri(source),
			Type = type,
			Time = DateTimeOffset.UtcNow,
			DataContentType = "application/json",
			Data = data,
		};

		cloudEvent.SetSchemaVersion(schemaVersion);
		cloudEvent.SetSchemaCompatibility(compatibility);

		return cloudEvent;
	}

	/// <summary>
	/// Checks if a CloudEvent is compatible with a specific schema version.
	/// </summary>
	public static bool IsCompatibleWith(this CloudEvent cloudEvent, string targetVersion, ISchemaRegistry? registry = null)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetVersion);

		var currentVersion = cloudEvent.GetSchemaVersion();
		if (string.IsNullOrEmpty(currentVersion))
		{
			// No version info - assume incompatible
			return false;
		}

		if (string.Equals(currentVersion, targetVersion, StringComparison.Ordinal))
		{
			// Exact match
			return true;
		}

		var compatibility = cloudEvent.GetSchemaCompatibility();
		if (compatibility == SchemaCompatibilityMode.None)
		{
			// No compatibility info - versions must match exactly
			return false;
		}

		// If we have a schema registry, use it for compatibility checking
		if (registry != null)
		{
			return registry.IsCompatible(
				cloudEvent.Type,
				currentVersion,
				targetVersion,
				compatibility);
		}

		// Basic version comparison without registry
		return IsVersionCompatible(currentVersion, targetVersion, compatibility);
	}

	private static bool IsVersionCompatible(string currentVersion, string targetVersion, SchemaCompatibilityMode mode)
	{
		// Simple semantic versioning check
		if (!TryParseVersion(currentVersion, out var current) ||
			!TryParseVersion(targetVersion, out var target))
		{
			return false;
		}

		return mode switch
		{
			SchemaCompatibilityMode.Forward => current.Major == target.Major && current.Minor <= target.Minor,
			SchemaCompatibilityMode.Backward => current.Major == target.Major && current.Minor >= target.Minor,
			SchemaCompatibilityMode.Full => current.Major == target.Major,
			_ => false,
		};
	}

	private static bool TryParseVersion(string version, out Version result)
	{
		// Support both "1.0" and "v1.0" formats
		var versionString = version.StartsWith('v')
			? version.Substring(1)
			: version;

		return Version.TryParse(versionString, out result!);
	}
}
