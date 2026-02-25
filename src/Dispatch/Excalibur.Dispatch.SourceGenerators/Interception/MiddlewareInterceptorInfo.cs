// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Interception;

/// <summary>
/// Contains metadata about a middleware type implementing IDispatchMiddleware for registry generation.
/// </summary>
/// <remarks>
/// PERF-10: Middleware invocation interceptors eliminate interface dispatch overhead by
/// generating typed invoker delegates for each middleware type.
/// </remarks>
internal sealed class MiddlewareInterceptorInfo
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
	/// Gets or sets the namespace of the middleware type.
	/// </summary>
	public string Namespace { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has the [AppliesTo] attribute.
	/// </summary>
	public bool HasAppliesToAttribute { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware has the [ExcludeKinds] attribute.
	/// </summary>
	public bool HasExcludeKindsAttribute { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the middleware overrides the Stage property.
	/// </summary>
	public bool OverridesStage { get; set; }

	/// <summary>
	/// Gets a safe identifier for the middleware type (for generated code).
	/// </summary>
	public string SafeIdentifier => MiddlewareTypeName.Replace(".", "_").Replace("+", "_");

	/// <summary>
	/// Determines if two MiddlewareInterceptorInfo instances represent the same middleware type.
	/// </summary>
	public bool Equals(MiddlewareInterceptorInfo? other)
	{
		if (other is null)
		{
			return false;
		}

		return MiddlewareTypeFullName == other.MiddlewareTypeFullName;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as MiddlewareInterceptorInfo);

	/// <inheritdoc />
	public override int GetHashCode() => MiddlewareTypeFullName.GetHashCode();
}
