// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.Execution;

namespace Excalibur.Data.SqlServer.Execution;

public static class DataExecutorExtensions
{
	/// <summary>
	/// </summary>
	/// <param name="executor"> </param>
	/// <param name="request"> </param>
	/// <param name="cancellationToken"> </param>
	/// <returns> A <see cref="Task{TResult}" /> representing the result of the asynchronous operation. </returns>
	public static Task<int> ExecuteAsync(this IDataExecutor executor, DataCommandRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(executor);
		ArgumentNullException.ThrowIfNull(request);

		return executor.ExecuteAsync(request.CommandText, request.Parameters, cancellationToken);
	}
}
