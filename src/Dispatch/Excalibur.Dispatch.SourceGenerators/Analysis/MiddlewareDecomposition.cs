// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Analysis;

/// <summary>
/// Represents the decomposition result of a middleware's InvokeAsync method into
/// Before and After phases for static pipeline generation.
/// </summary>
/// <remarks>
/// <para>
/// Middleware decomposition is the key transformation for static pipeline generation.
/// </para>
/// <para>
/// A middleware that wraps `next()` like this:
/// <code>
/// public async Task&lt;IMessageResult&gt; InvokeAsync(...)
/// {
///     var stopwatch = Stopwatch.StartNew();  // BEFORE
///     var result = await next(...);          // NEXT
///     _logger.Log(stopwatch.Elapsed);        // AFTER
///     return result;
/// }
/// </code>
/// Gets decomposed into:
/// <list type="bullet">
/// <item>State record: <c>record struct {Middleware}_State(Stopwatch Stopwatch)</c></item>
/// <item>Before method: Returns (State, ShouldContinue) tuple</item>
/// <item>After method: Void, takes result and state</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MiddlewareDecomposition
{
	/// <summary>
	/// Gets or sets the middleware type symbol.
	/// </summary>
	public INamedTypeSymbol MiddlewareType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the fully qualified middleware type name.
	/// </summary>
	public string MiddlewareTypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the simple middleware type name.
	/// </summary>
	public string MiddlewareTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether this middleware can be decomposed
	/// into Before/After phases for static inlining.
	/// </summary>
	public bool IsDecomposable { get; set; }

	/// <summary>
	/// Gets or sets the reason why the middleware cannot be decomposed.
	/// </summary>
	public string? NonDecomposableReason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has before-phase logic.
	/// </summary>
	public bool HasBeforePhase { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has after-phase logic.
	/// </summary>
	public bool HasAfterPhase { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has a try/catch pattern
	/// that needs to be preserved in the static pipeline.
	/// </summary>
	public bool HasTryCatch { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has a finally block
	/// that needs to run even on exceptions.
	/// </summary>
	public bool HasFinally { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware uses a using statement
	/// that needs disposal tracking.
	/// </summary>
	public bool HasUsing { get; set; }

	/// <summary>
	/// Gets or sets the list of state variables that need to be captured
	/// from the Before phase and passed to the After phase.
	/// </summary>
	public List<StateVariable> StateVariables { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the middleware can short-circuit
	/// the pipeline by returning early without calling next().
	/// </summary>
	public bool CanShortCircuit { get; set; }

	/// <summary>
	/// Gets or sets the middleware stage for ordering in the pipeline.
	/// </summary>
	public int? Stage { get; set; }

	/// <summary>
	/// Gets or sets the message kinds this middleware applies to.
	/// </summary>
	public string? ApplicableMessageKinds { get; set; }

	/// <summary>
	/// Gets a safe identifier for the middleware (for generated code).
	/// </summary>
	public string SafeIdentifier => MiddlewareTypeName.Replace(".", "_").Replace("+", "_");

	/// <summary>
	/// Determines if two MiddlewareDecomposition instances represent the same middleware.
	/// </summary>
	public bool Equals(MiddlewareDecomposition? other)
	{
		if (other is null)
		{
			return false;
		}

		return MiddlewareTypeFullName == other.MiddlewareTypeFullName;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as MiddlewareDecomposition);

	/// <inheritdoc />
	public override int GetHashCode() => MiddlewareTypeFullName.GetHashCode();
}

/// <summary>
/// Represents a state variable that needs to be captured from Before phase
/// and passed to After phase during static pipeline execution.
/// </summary>
internal sealed class StateVariable
{
	/// <summary>
	/// Gets or sets the variable name.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the fully qualified type name of the variable.
	/// </summary>
	public string TypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the variable is nullable.
	/// </summary>
	public bool IsNullable { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the variable requires disposal.
	/// </summary>
	public bool RequiresDisposal { get; set; }
}
