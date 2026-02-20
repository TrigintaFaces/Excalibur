// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Workflows;

/// <summary>
/// Provides context information for workflow job execution.
/// </summary>
/// <typeparam name="TInput"> The type of input data for the workflow. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="WorkflowJobContext{TInput}" /> class. </remarks>
/// <param name="instanceId"> The unique identifier for this workflow execution instance. </param>
/// <param name="input"> The input data for the workflow. </param>
/// <param name="correlationId"> The correlation identifier for tracking across services. </param>
public sealed class WorkflowJobContext<TInput>(string instanceId, TInput input, string? correlationId)
{
	/// <summary>
	/// Gets the unique identifier for this workflow execution instance.
	/// </summary>
	/// <value>
	/// The unique identifier for this workflow execution instance.
	/// </value>
	public string InstanceId { get; } = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

	/// <summary>
	/// Gets the input data for the workflow.
	/// </summary>
	/// <value>
	/// The input data for the workflow.
	/// </value>
	public TInput Input { get; } = input;

	/// <summary>
	/// Gets the correlation identifier for tracking across services.
	/// </summary>
	/// <value>
	/// The correlation identifier for tracking across services.
	/// </value>
	public string? CorrelationId { get; } = correlationId;
}
