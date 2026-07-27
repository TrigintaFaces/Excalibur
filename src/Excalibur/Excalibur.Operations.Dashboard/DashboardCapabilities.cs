// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// Describes which dashboard subsystems are available in the running host and whether mutating
/// actions are enabled, so the single-page app can render only the panels backed by a configured
/// subsystem (fail-open capability discovery).
/// </summary>
public sealed class DashboardCapabilities
{
	/// <summary>
	/// Gets the stable keys of the subsystems whose read endpoints are mapped (for example
	/// <c>outbox</c>, <c>dlq</c>, <c>saga</c>, <c>leader</c>).
	/// </summary>
	/// <value> The available subsystem keys. </value>
	public IReadOnlyList<string> Subsystems { get; init; } = [];

	/// <summary>
	/// Gets a value indicating whether mutating actions (dead-letter replay) are enabled and mapped.
	/// </summary>
	/// <value> <see langword="true"/> when mutating endpoints are mapped; otherwise <see langword="false"/>. </value>
	public bool MutatingActionsEnabled { get; init; }
}
