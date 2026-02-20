// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Queries;

/// <summary>
/// Configuration options for saga correlation queries.
/// </summary>
public sealed class SagaCorrelationQueryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of results to return per query.
	/// </summary>
	/// <value>The maximum result count, default is 100.</value>
	[Range(1, 10000)]
	public int MaxResults { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to include completed sagas in query results.
	/// </summary>
	/// <value><see langword="true"/> to include completed sagas; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool IncludeCompleted { get; set; }
}
