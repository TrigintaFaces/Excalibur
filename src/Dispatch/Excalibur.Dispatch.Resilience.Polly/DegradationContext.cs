// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Context for degradation operations.
/// </summary>
/// <typeparam name="T">The type of result returned by the operation.</typeparam>
public sealed class DegradationContext<T>
{
	/// <summary>
	/// Gets the primary operation to execute.
	/// </summary>
	/// <value>The asynchronous delegate invoked before any degradation fallback is considered.</value>
	public required Func<Task<T>> PrimaryOperation { get; init; }

	/// <summary>
	/// Gets fallback operations for each degradation level.
	/// </summary>
	/// <value>A dictionary mapping each degradation level to the corresponding fallback operation.</value>
	public IReadOnlyDictionary<DegradationLevel, Func<Task<T>>> Fallbacks { get; init; } = new Dictionary<DegradationLevel, Func<Task<T>>>();

	/// <summary>
	/// Gets the operation name for tracking.
	/// </summary>
	/// <value>The descriptive name used when emitting metrics and logs. Defaults to "Unknown".</value>
	public string OperationName { get; init; } = "Unknown";

	/// <summary>
	/// Gets the priority of the operation (higher = more important).
	/// </summary>
	/// <value>An integer ranking that influences scheduling when resources are constrained.</value>
	public int Priority { get; init; }

	/// <summary>
	/// Gets a value indicating whether this operation is critical.
	/// </summary>
	/// <value><see langword="true"/> when the operation must complete even during severe degradation; otherwise, <see langword="false"/>.</value>
	public bool IsCritical { get; init; }

	/// <summary>
	/// Gets custom metadata for the operation.
	/// </summary>
	/// <value>A read-only dictionary of additional metadata appended to emitted events.</value>
	public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
