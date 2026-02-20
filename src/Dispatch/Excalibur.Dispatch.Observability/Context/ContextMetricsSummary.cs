// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Summary of context flow metrics.
/// </summary>
public sealed class ContextMetricsSummary
{
	/// <summary>
	/// Gets or sets total contexts processed.
	/// </summary>
	public long TotalContextsProcessed { get; set; }

	/// <summary>
	/// Gets or sets contexts preserved successfully.
	/// </summary>
	public long ContextsPreservedSuccessfully { get; set; }

	/// <summary>
	/// Gets or sets the preservation rate (0-1).
	/// </summary>
	public double PreservationRate { get; set; }

	/// <summary>
	/// Gets or sets currently active contexts.
	/// </summary>
	public long ActiveContexts { get; set; }

	/// <summary>
	/// Gets or sets maximum lineage depth observed.
	/// </summary>
	public long MaxLineageDepth { get; set; }

	/// <summary>
	/// Gets or sets when this summary was generated.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }
}
