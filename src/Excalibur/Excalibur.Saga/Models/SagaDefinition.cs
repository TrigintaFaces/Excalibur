// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Models;

/// <summary>
/// Defines the structure and configuration of a saga.
/// </summary>
/// <typeparam name="TData"> The type of data for the saga. </typeparam>
public sealed class SagaDefinition<TData>
	where TData : class
{
	/// <summary>
	/// Gets or sets the name of the saga.
	/// </summary>
	/// <value>the name of the saga.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the version of the saga definition.
	/// </summary>
	/// <value>the version of the saga definition.</value>
	public string Version { get; set; } = "1.0";

	/// <summary>
	/// Gets or sets the description of the saga.
	/// </summary>
	/// <value>the description of the saga.</value>
	public string? Description { get; set; }

	/// <summary>
	/// Gets the list of steps in the saga.
	/// </summary>
	/// <value>the list of steps in the saga.</value>
	public IList<ISagaStep<TData>> Steps { get; } = [];

	/// <summary>
	/// Gets or sets the overall timeout for the saga.
	/// </summary>
	/// <value>the overall timeout for the saga.</value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the retention period for completed sagas.
	/// </summary>
	/// <value>the retention period for completed sagas.</value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets a value indicating whether to enable caching for this saga.
	/// </summary>
	/// <value><see langword="true"/> if to enable caching for this saga.; otherwise, <see langword="false"/>.</value>
	public bool EnableCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets the cache TTL for saga state.
	/// </summary>
	/// <value>the cache TTL for saga state.</value>
	public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets custom metadata for the saga.
	/// </summary>
	/// <value>custom metadata for the saga.</value>
	public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

