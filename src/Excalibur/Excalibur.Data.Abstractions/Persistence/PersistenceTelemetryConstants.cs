// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Shared OpenTelemetry constants for persistence provider telemetry.
/// </summary>
/// <remarks>
/// <para>
/// Follows the shared-constants pattern from <c>GooglePubSubTelemetryConstants</c>:
/// single <see cref="Meter"/> and <see cref="ActivitySource"/> shared across all
/// persistence provider decorators and implementations.
/// </para>
/// </remarks>
public static class PersistenceTelemetryConstants
{
	/// <summary>
	/// The name used for the <see cref="ActivitySource"/> and <see cref="Meter"/>.
	/// </summary>
	public const string SourceName = "Excalibur.Data.Persistence";

	/// <summary>
	/// The version string for the telemetry source.
	/// </summary>
	public const string Version = "1.0.0";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for persistence operations.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(SourceName, Version);

	/// <summary>
	/// Shared <see cref="Meter"/> for persistence metrics.
	/// </summary>
	public static Meter Meter { get; } = new(SourceName, Version);

	// Semantic attribute names
	/// <summary>The provider name attribute.</summary>
	public const string AttributeProviderName = "persistence.provider.name";

	/// <summary>The provider type attribute (SQL, Document, KeyValue).</summary>
	public const string AttributeProviderType = "persistence.provider.type";

	/// <summary>The operation name attribute.</summary>
	public const string AttributeOperation = "persistence.operation";

	/// <summary>The request type attribute (full type name of IDataRequest).</summary>
	public const string AttributeRequestType = "persistence.request.type";
}
