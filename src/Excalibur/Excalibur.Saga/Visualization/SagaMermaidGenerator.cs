// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Visualization;

/// <summary>
/// Generates Mermaid stateDiagram-v2 diagrams from saga definitions.
/// Steps are rendered as states with transitions between them. Compensation
/// paths are shown as dashed arrows. Parallel steps use fork/join notation.
/// </summary>
/// <typeparam name="TData">The type of data flowing through the saga.</typeparam>
public sealed class SagaMermaidGenerator<TData> : ISagaVisualizer<TData>
	where TData : class
{
	/// <inheritdoc/>
	public string GenerateDiagram(SagaDefinition<TData> definition, SagaVisualizationFormat format)
	{
		ArgumentNullException.ThrowIfNull(definition);

		if (format != SagaVisualizationFormat.Mermaid)
		{
			throw new NotSupportedException($"Format '{format}' is not supported by {nameof(SagaMermaidGenerator<>)}. Use {nameof(SagaVisualizationFormat.Mermaid)}.");
		}

		return GenerateMermaid(definition);
	}

	private static string GenerateMermaid(SagaDefinition<TData> definition)
	{
		var sb = new StringBuilder();

		sb.AppendLine("stateDiagram-v2");

		var sagaName = string.IsNullOrEmpty(definition.Name)
			? "Saga"
			: definition.Name;

		sb.AppendLine($"    %% {sagaName} (v{definition.Version})");

		if (definition.Steps.Count == 0)
		{
			sb.AppendLine("    [*] --> [*] : No steps defined");
			return sb.ToString();
		}

		// Emit step state definitions
		for (var i = 0; i < definition.Steps.Count; i++)
		{
			var step = definition.Steps[i];
			var stateId = SanitizeStateId(step.Name);

			EmitStepState(sb, step, stateId);
		}

		sb.AppendLine();

		// Emit forward transitions: [*] -> step1 -> step2 -> ... -> [*]
		var firstStep = definition.Steps[0];
		sb.AppendLine($"    [*] --> {SanitizeStateId(firstStep.Name)}");

		for (var i = 0; i < definition.Steps.Count - 1; i++)
		{
			var current = definition.Steps[i];
			var next = definition.Steps[i + 1];
			var currentId = SanitizeStateId(current.Name);
			var nextId = SanitizeStateId(next.Name);

			if (current is IConditionalSagaStep<TData> conditional)
			{
				EmitConditionalTransitions(sb, conditional, currentId, nextId);
			}
			else
			{
				sb.AppendLine($"    {currentId} --> {nextId}");
			}
		}

		var lastStep = definition.Steps[definition.Steps.Count - 1];
		sb.AppendLine($"    {SanitizeStateId(lastStep.Name)} --> [*]");

		sb.AppendLine();

		// Emit compensation paths (dashed arrows going backwards)
		sb.AppendLine("    %% Compensation paths");
		for (var i = definition.Steps.Count - 1; i >= 0; i--)
		{
			var step = definition.Steps[i];
			if (!step.CanCompensate)
			{
				continue;
			}

			var stateId = SanitizeStateId(step.Name);
			var compensateId = stateId + "_compensate";

			sb.AppendLine($"    state \"{step.Name} (Compensate)\" as {compensateId}");
			sb.AppendLine($"    {stateId} --> {compensateId} : failure");

			// Link compensation back to a previous step or to failure end state
			if (i > 0)
			{
				var prevStep = definition.Steps[i - 1];
				if (prevStep.CanCompensate)
				{
					var prevCompensateId = SanitizeStateId(prevStep.Name) + "_compensate";
					sb.AppendLine($"    {compensateId} --> {prevCompensateId}");
				}
				else
				{
					sb.AppendLine($"    {compensateId} --> [*]");
				}
			}
			else
			{
				sb.AppendLine($"    {compensateId} --> [*]");
			}
		}

		return sb.ToString();
	}

	private static void EmitStepState(StringBuilder sb, ISagaStep<TData> step, string stateId)
	{
		if (step is IParallelSagaStep<TData> parallel)
		{
			sb.AppendLine($"    state {stateId} {{");
			sb.AppendLine($"        [*] --> {stateId}_fork");
			sb.AppendLine($"        state {stateId}_fork <<fork>>");

			foreach (var childStep in parallel.ParallelSteps)
			{
				var childId = SanitizeStateId(childStep.Name);
				sb.AppendLine($"        {stateId}_fork --> {childId}");
			}

			sb.AppendLine($"        state {stateId}_join <<join>>");

			foreach (var childStep in parallel.ParallelSteps)
			{
				var childId = SanitizeStateId(childStep.Name);
				sb.AppendLine($"        {childId} --> {stateId}_join");
			}

			sb.AppendLine($"        {stateId}_join --> [*]");
			sb.AppendLine("    }");
		}
		else
		{
			var annotation = step.CanCompensate ? " (compensatable)" : "";
			var timeoutNote = step.Timeout != TimeSpan.Zero
				? $" [timeout: {step.Timeout.TotalSeconds}s]"
				: "";

			sb.AppendLine($"    state \"{step.Name}{annotation}{timeoutNote}\" as {stateId}");
		}
	}

	private static void EmitConditionalTransitions(
		StringBuilder sb,
		IConditionalSagaStep<TData> conditional,
		string currentId,
		string nextId)
	{
		if (conditional.ThenStep is not null)
		{
			var thenId = SanitizeStateId(conditional.ThenStep.Name);
			sb.AppendLine($"    {currentId} --> {thenId} : condition = true");
			sb.AppendLine($"    {thenId} --> {nextId}");
		}

		if (conditional.ElseStep is not null)
		{
			var elseId = SanitizeStateId(conditional.ElseStep.Name);
			sb.AppendLine($"    {currentId} --> {elseId} : condition = false");
			sb.AppendLine($"    {elseId} --> {nextId}");
		}

		if (conditional.ThenStep is null && conditional.ElseStep is null)
		{
			sb.AppendLine($"    {currentId} --> {nextId}");
		}
	}

	private static string SanitizeStateId(string name) =>
		name.Replace(" ", "_", StringComparison.Ordinal)
			.Replace("-", "_", StringComparison.Ordinal)
			.Replace(".", "_", StringComparison.Ordinal);
}
