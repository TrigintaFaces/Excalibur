// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// A filter and paging window for an administrative workflow-instance query.
/// </summary>
/// <remarks>
/// Unset filter properties (<see langword="null"/>) match any value. Paging is required so a large store is
/// never enumerated unbounded; implementations apply a default page size when <see cref="Take"/> is not a
/// positive value.
/// </remarks>
public sealed record WorkflowQueryFilter
{
	/// <summary>
	/// Gets the lifecycle status to filter by, or <see langword="null"/> to match any status.
	/// </summary>
	/// <value>The status filter, or <see langword="null"/> for any.</value>
	public WorkflowStatus? Status { get; init; }

	/// <summary>
	/// Gets the workflow name to filter by, or <see langword="null"/> to match any workflow.
	/// </summary>
	/// <value>The workflow-name filter, or <see langword="null"/> for any.</value>
	public string? WorkflowName { get; init; }

	/// <summary>
	/// Gets the number of matching instances to skip before the returned page.
	/// </summary>
	/// <value>The zero-based paging offset.</value>
	public int Skip { get; init; }

	/// <summary>
	/// Gets the maximum number of instances to return in the page.
	/// </summary>
	/// <value>The page size; implementations apply a default when this is not positive.</value>
	public int Take { get; init; }
}
