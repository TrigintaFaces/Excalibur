// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Coordination;

/// <summary>
/// Manages registration and discovery of job processing instances.
/// </summary>
public interface IJobRegistry
{
	/// <summary>
	/// Registers the current instance as available for job processing.
	/// </summary>
	/// <param name="instanceId"> The unique identifier for this instance. </param>
	/// <param name="instanceInfo"> Information about this instance's capabilities. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RegisterInstanceAsync(string instanceId, JobInstanceInfo instanceInfo, CancellationToken cancellationToken);

	/// <summary>
	/// Unregisters the current instance from job processing.
	/// </summary>
	/// <param name="instanceId"> The unique identifier for this instance. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all currently active instances that can process jobs.
	/// </summary>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A task containing the list of active job processing instances. </returns>
	Task<IEnumerable<JobInstanceInfo>> GetActiveInstancesAsync(CancellationToken cancellationToken);
}
