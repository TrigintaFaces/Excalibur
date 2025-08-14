// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Defines the contract for a Change Data Capture (CDC) processor, which processes CDC changes and delegates handling to a specified
///     event handler.
/// </summary>
public interface ICdcProcessor : IAsyncDisposable
{
	/// <summary>
	///     Processes CDC changes and invokes the specified event handler for each batch of data change events.
	/// </summary>
	/// <param name="eventHandler"> A delegate that handles a <see cref="DataChangeEvent" /> instance. </param>
	/// <param name="cancellationToken">
	///     A token to observe while waiting for the task to complete. This token can be used to cancel the processing operation.
	/// </param>
	/// <returns>
	///     A task representing the asynchronous operation. The task result contains the total number of events processed successfully.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="eventHandler" /> is null. </exception>
	/// <exception cref="OperationCanceledException"> Thrown if the operation is canceled via the <paramref name="cancellationToken" />. </exception>
	public Task<int> ProcessCdcChangesAsync(Func<DataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);
}
