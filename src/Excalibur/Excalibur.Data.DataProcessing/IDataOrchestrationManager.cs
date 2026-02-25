// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Defines methods for managing and processing data tasks for different record types.
/// </summary>
public interface IDataOrchestrationManager
{
	/// <summary>
	/// Adds a new data task for a specific record type.
	/// </summary>
	/// <param name="recordType"> The type of record for which the data task is created. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A <see cref="Guid" /> representing the unique identifier of the created data task. </returns>
	Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken);

	/// <summary>
	/// Processes all pending data tasks.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken);
}
