// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// A null implementation of <see cref="IMetrics" /> that performs no operations.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is used when metrics collection is disabled or not configured, providing a performance-optimized no-op
/// implementation that avoids any overhead from metrics recording while maintaining the same API contract.
/// </para>
/// <para>
/// The null object pattern implementation ensures that serialization components can safely call metrics methods without null checks while
/// incurring zero cost when telemetry is not needed.
/// </para>
/// </remarks>
internal sealed class NullMetrics : IMetrics
{
	/// <summary>
	/// Gets the singleton instance of the null metrics implementation.
	/// </summary>
	/// <remarks>
	/// Using a singleton ensures minimal memory allocation and provides a shared instance across all serialization components that need a
	/// no-op metrics provider.
	/// </remarks>
	public static readonly NullMetrics Instance = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="NullMetrics"/> class.
	/// Prevents external instantiation of the null metrics implementation.
	/// </summary>
	private NullMetrics()
	{
	}

	/// <inheritdoc />
	/// <remarks> This implementation performs no operation and returns immediately with no overhead. </remarks>
	public void RecordCounter(string name, long value, params KeyValuePair<string, object?>[] tags)
	{
	}

	/// <inheritdoc />
	/// <remarks> This implementation performs no operation and returns immediately with no overhead. </remarks>
	public void RecordGauge(string name, double value, params KeyValuePair<string, object?>[] tags)
	{
	}
}
