// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Interception;

/// <summary>
/// Contains metadata about a DispatchAsync call site that can be intercepted.
/// </summary>
internal sealed class InterceptorInfo
{
	/// <summary>
	/// Gets or sets the interceptable location data from Roslyn.
	/// This is the new approach required by .NET 9+ Roslyn (replaces file path/line/column strings).
	/// </summary>
	public string? InterceptableLocationData { get; set; }

	/// <summary>
	/// Gets or sets the file path where the call site is located (for unique ID generation).
	/// </summary>
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the line number of the call site (1-based, for unique ID generation).
	/// </summary>
	public int Line { get; set; }

	/// <summary>
	/// Gets or sets the column number of the call site (1-based, for unique ID generation).
	/// </summary>
	public int Column { get; set; }

	/// <summary>
	/// Gets or sets the message type being dispatched.
	/// </summary>
	public ITypeSymbol MessageType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the fully qualified message type name.
	/// </summary>
	public string MessageTypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the simple message type name.
	/// </summary>
	public string MessageTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the handler type that handles this message.
	/// </summary>
	public ITypeSymbol? HandlerType { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified handler type name.
	/// </summary>
	public string? HandlerTypeFullName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the handler returns a result.
	/// </summary>
	public bool HasResult { get; set; }

	/// <summary>
	/// Gets or sets the result type if the handler returns one.
	/// </summary>
	public ITypeSymbol? ResultType { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified result type name.
	/// </summary>
	public string? ResultTypeFullName { get; set; }

	/// <summary>
	/// Gets a unique identifier for the interceptor method name.
	/// </summary>
	public string UniqueId => $"{MessageTypeName}_{Line}_{Column}";
}
