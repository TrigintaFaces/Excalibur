// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Marks a class or method as a durable workflow definition, whose body executes under deterministic
/// replay driven by an <see cref="IWorkflowContext"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class WorkflowAttribute : Attribute
{
	/// <summary>
	/// Gets the optional explicit workflow name used for registration and journaling. When omitted, the
	/// annotated type or method name is used.
	/// </summary>
	/// <value>The explicit workflow name, or <see langword="null"/> to use the member name.</value>
	public string? Name { get; init; }
}
