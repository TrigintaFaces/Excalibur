// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.BoundedContext;

/// <summary>
/// Configuration options for bounded context enforcement.
/// </summary>
/// <remarks>
/// Follows the <c>IOptions&lt;T&gt;</c> pattern from <c>Microsoft.Extensions.Options</c>.
/// Property count: 3 (within the ≤10-property quality gate).
/// </remarks>
public class BoundedContextOptions
{
	/// <summary>
	/// Gets or sets the enforcement mode for boundary violations.
	/// </summary>
	/// <value>
	/// The enforcement mode. Defaults to <see cref="BoundedContextEnforcementMode.Warn"/>.
	/// </value>
	public BoundedContextEnforcementMode EnforcementMode { get; set; } = BoundedContextEnforcementMode.Warn;

	/// <summary>
	/// Gets the patterns that are allowed to cross bounded context boundaries.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Some cross-boundary references are intentional (e.g., shared value objects, integration events).
	/// Add patterns to this collection to suppress violations for specific source-target context pairs.
	/// </para>
	/// <para>
	/// Format: <c>"SourceContext-&gt;TargetContext"</c> (e.g., <c>"Orders-&gt;SharedKernel"</c>).
	/// </para>
	/// </remarks>
	/// <value>The collection of allowed cross-boundary patterns.</value>
	public IList<string> AllowedCrossBoundaryPatterns { get; } = new List<string>();

	/// <summary>
	/// Gets or sets a value indicating whether to scan assemblies at startup for violations.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to run validation at startup; otherwise, <see langword="false"/>.
	/// Defaults to <see langword="false"/>.
	/// </value>
	public bool ValidateOnStartup { get; set; }
}
