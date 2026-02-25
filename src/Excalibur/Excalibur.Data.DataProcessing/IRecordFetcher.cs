// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Defines a contract for fetching records asynchronously.
/// </summary>
/// <typeparam name="TRecord"> The type of the records being fetched. </typeparam>
public interface IRecordFetcher<TRecord>
{
	/// <summary>
	/// Fetches all records asynchronously, starting from the specified position.
	/// </summary>
	/// <param name="skip">
	/// The number of records to skip before starting the fetch. Useful for resuming operations or implementing paging.
	/// </param>
	/// <param name="batchSize"> The number of records to fetch in this batch. </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns> An asynchronous enumerable of records of type <typeparamref name="TRecord" />. </returns>
	Task<IEnumerable<TRecord>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken);
}
