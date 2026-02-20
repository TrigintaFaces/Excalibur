// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Attributes;

/// <summary>
/// Marks a method as a saga compensation handler for source generator discovery.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to methods in a saga class to declare them as
/// compensation handlers. Each compensation method is associated with
/// a forward step identified by <see cref="ForStep"/>.
/// </para>
/// <para>
/// Compensation methods are invoked in reverse order when a saga step
/// fails, undoing the effects of previously completed steps.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SagaCompensation(ForStep = "ReserveInventory", Order = 1)]
/// public async Task ReleaseInventoryAsync(OrderData data, CancellationToken ct)
/// {
///     // compensation implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SagaCompensationAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the name of the step this method compensates.
	/// </summary>
	/// <value>
	/// The step name matching the <see cref="SagaStepAttribute.StepName"/>
	/// of the associated forward step.
	/// </value>
	public string ForStep { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the compensation execution order.
	/// </summary>
	/// <value>
	/// The order in which this compensation runs relative to other compensations.
	/// Default is 0, which means compensation runs in reverse step order.
	/// </value>
	public int Order { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for this compensation step.
	/// </summary>
	/// <value>
	/// The maximum retry attempts. A value of -1 indicates that the
	/// <see cref="AdvancedSagaOptions.MaxRetryAttempts"/> default should be used.
	/// Default is -1.
	/// </value>
	public int MaxRetries { get; set; } = -1;

	/// <summary>
	/// Gets or sets the compensation strategy for this step.
	/// </summary>
	/// <value>
	/// The <see cref="Saga.CompensationStrategy"/> to use. Default is
	/// <see cref="CompensationStrategy.Default"/>, which defers to
	/// <see cref="AdvancedSagaOptions.EnableAutoCompensation"/>.
	/// </value>
	public CompensationStrategy Strategy { get; set; } = CompensationStrategy.Default;
}
