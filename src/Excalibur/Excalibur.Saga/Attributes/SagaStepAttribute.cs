// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Attributes;

/// <summary>
/// Marks a method as a saga step for source generator discovery.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to methods in a saga class to declare them as
/// forward execution steps. The source generator uses these attributes
/// to produce step registration code automatically.
/// </para>
/// <para>
/// Steps are executed in ascending <see cref="Order"/> sequence.
/// Each step should have a corresponding compensation method marked
/// with <see cref="SagaCompensationAttribute"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SagaStep(StepName = "ReserveInventory", Order = 1, TimeoutSeconds = 30)]
/// public async Task ReserveInventoryAsync(OrderData data, CancellationToken ct)
/// {
///     // step implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SagaStepAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the display name for this step.
	/// </summary>
	/// <value>
	/// The step name. If not specified, defaults to the method name
	/// with any "Async" suffix removed.
	/// </value>
	public string? StepName { get; set; }

	/// <summary>
	/// Gets or sets the execution order for this step (1-based).
	/// </summary>
	/// <value>The step order. Steps execute in ascending order.</value>
	public int Order { get; set; }

	/// <summary>
	/// Gets or sets the per-step timeout in seconds.
	/// </summary>
	/// <value>
	/// The timeout in seconds. A value of 0 indicates that the saga-level
	/// timeout should be used. Default is 0.
	/// </value>
	public int TimeoutSeconds { get; set; }
}
