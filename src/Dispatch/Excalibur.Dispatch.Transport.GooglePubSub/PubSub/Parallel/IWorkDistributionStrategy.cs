// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the strategy for distributing work across parallel processors.
/// </summary>
public interface IWorkDistributionStrategy
{
	/// <summary>
	/// Selects the worker ID for a given work item.
	/// </summary>
	/// <param name="context"> Context containing work item details. </param>
	/// <returns> The selected worker ID. </returns>
	int SelectWorker(WorkDistributionContext context);

	/// <summary>
	/// Updates strategy state after work completion.
	/// </summary>
	/// <param name="workerId"> The worker that completed the work. </param>
	/// <param name="duration"> Duration of the work. </param>
	void RecordCompletion(int workerId, TimeSpan duration);
}
