// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Represents a mutation detected in the context.
/// </summary>
internal sealed class ContextMutation
{
	/// <summary>
	/// Gets or sets the field that was mutated.
	/// </summary>
	public required string Field { get; set; }

	/// <summary>
	/// Gets or sets the type of mutation.
	/// </summary>
	public required MutationType Type { get; set; }

	/// <summary>
	/// Gets or sets the old value before mutation.
	/// </summary>
	public object? OldValue { get; set; }

	/// <summary>
	/// Gets or sets the new value after mutation.
	/// </summary>
	public object? NewValue { get; set; }

	/// <summary>
	/// Gets or sets the pipeline stage where mutation occurred.
	/// </summary>
	public required string Stage { get; set; }
}
