// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Defines the base contract for all metric types in the messaging system.
/// </summary>
/// <remarks>
/// <para>
/// This interface serves as the foundation for all metric implementations, providing common metadata access. All metrics (counters, gauges,
/// histograms, etc.) implement this interface to ensure consistent behavior across the telemetry infrastructure.
/// </para>
/// <para>
/// The interface enables:
/// - Uniform access to metric metadata (name, description, labels)
/// - Polymorphic handling of different metric types
/// - Registration and management in metric registries
/// - Consistent serialization and export across telemetry systems.
/// </para>
/// </remarks>
public interface IMetric
{
	/// <summary>
	/// Gets the metadata describing this metric instance.
	/// </summary>
	/// <value>
	/// A <see cref="MetricMetadata" /> instance containing the metric's name, description, type, labels, and other descriptive information
	/// used for identification and export.
	/// </value>
	/// <remarks>
	/// The metadata provides essential information for metric systems to properly identify, categorize, and export metrics. This includes
	/// the metric name for time series identification, human-readable descriptions for observability tools, and label sets for dimensional analysis.
	/// </remarks>
	MetricMetadata? Metadata { get; }
}
