// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Filter criteria for querying saga instances via <see cref="ISagaStoreAdmin.QuerySagasAsync"/>.
/// </summary>
public sealed class SagaQueryFilter
{
	/// <summary>The default page size when <see cref="MaxResults"/> is not specified.</summary>
	internal const int DefaultMaxResults = 100;

	/// <summary>
	/// The hard upper bound on <see cref="MaxResults"/>. Requests above this are clamped down so a
	/// query can never translate to an unbounded <c>LIMIT</c>/<c>Take</c> across the store providers.
	/// </summary>
	internal const int MaxAllowedResults = 1000;

	private readonly int _maxResults = DefaultMaxResults;
	private readonly int _skip;

	/// <summary>
	/// Gets the completion-state filter: <see langword="false"/> for running sagas, <see langword="true"/>
	/// for completed sagas, or <see langword="null"/> for both.
	/// </summary>
	/// <value>The completion-state filter, or <see langword="null"/> for no filter.</value>
	public bool? IsCompleted { get; init; }

	/// <summary>Gets the tenant filter, or <see langword="null"/> to include all tenants.</summary>
	/// <value>The tenant id filter, or <see langword="null"/>.</value>
	public string? TenantId { get; init; }

	/// <summary>Gets the number of matching instances to skip (for pagination).</summary>
	/// <value>The skip count, clamped to a minimum of <c>0</c>; defaults to <c>0</c>.</value>
	public int Skip
	{
		get => _skip;
		init => _skip = value < 0 ? 0 : value;
	}

	/// <summary>Gets the maximum number of instances to return.</summary>
	/// <remarks>
	/// The value is clamped to the range <c>[1, </c><see cref="MaxAllowedResults"/><c>]</c> so a query
	/// can never request an unbounded result set, regardless of the underlying store provider.
	/// </remarks>
	/// <value>The page size; defaults to <see cref="DefaultMaxResults"/>.</value>
	public int MaxResults
	{
		get => _maxResults;
		init => _maxResults = value < 1 ? 1 : (value > MaxAllowedResults ? MaxAllowedResults : value);
	}
}
