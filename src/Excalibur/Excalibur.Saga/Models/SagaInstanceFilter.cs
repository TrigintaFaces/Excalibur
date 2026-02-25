// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Filter criteria for querying saga instances.
/// </summary>
public sealed class SagaInstanceFilter
{
	/// <summary>
	/// Gets or sets the saga definition ID to filter by.
	/// </summary>
	/// <value>the saga definition ID to filter by.</value>
	public string? SagaId { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID to filter by.
	/// </summary>
	/// <value>the correlation ID to filter by.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the status to filter by.
	/// </summary>
	/// <value>the status to filter by.</value>
	public SagaStatus? Status { get; set; }

	/// <summary>
	/// Gets or sets the minimum creation date.
	/// </summary>
	/// <value>the minimum creation date.</value>
	public DateTime? CreatedAfter { get; set; }

	/// <summary>
	/// Gets or sets the maximum creation date.
	/// </summary>
	/// <value>the maximum creation date.</value>
	public DateTime? CreatedBefore { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of results.
	/// </summary>
	/// <value>the maximum number of results.</value>
	public int? MaxResults { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include completed sagas.
	/// </summary>
	/// <value><see langword="true"/> if to include completed sagas.; otherwise, <see langword="false"/>.</value>
	public bool IncludeCompleted { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include failed sagas.
	/// </summary>
	/// <value><see langword="true"/> if to include failed sagas.; otherwise, <see langword="false"/>.</value>
	public bool IncludeFailed { get; set; } = true;

	/// <summary>
	/// Gets metadata filters.
	/// </summary>
	/// <value>metadata filters.</value>
	public IDictionary<string, string>? MetadataFilters { get; }
}

