// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Defines a contract for fetching records asynchronously using cursor-based pagination.
/// </summary>
/// <typeparam name="TRecord"> The type of the records being fetched. </typeparam>
public interface IRecordFetcher<TRecord>
{
	/// <summary>
	/// Fetches the next batch of records starting from the position identified by <paramref name="cursor"/>.
	/// </summary>
	/// <param name="cursor">
	/// An opaque cursor token indicating where to resume fetching, or <see langword="null"/>
	/// to start from the beginning. The value is produced by the previous call's
	/// <see cref="CursorFetchResult{TRecord}.NextCursor"/>.
	/// </param>
	/// <param name="batchSize"> The maximum number of records to fetch in this batch. </param>
	/// <param name="cancellationToken"> A token that can be used to cancel the operation. </param>
	/// <returns>
	/// A <see cref="CursorFetchResult{TRecord}"/> containing the fetched records and
	/// a cursor to the next page, or <see langword="null"/> <see cref="CursorFetchResult{TRecord}.NextCursor"/>
	/// when no more data is available.
	/// </returns>
	Task<CursorFetchResult<TRecord>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken);
}
