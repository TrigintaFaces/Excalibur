// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Pipeline;

/// <summary>
/// Contains metadata about a static pipeline chain for code generation.
/// </summary>
/// <remarks>
/// Represents a fully static middleware pipeline for a deterministic message type.
/// The chain contains ordered middleware decompositions that can be inlined at compile time.
/// </remarks>
internal sealed class PipelineChainInfo
{
	/// <summary>
	/// Gets or sets the message type symbol for this pipeline.
	/// </summary>
	public INamedTypeSymbol MessageType { get; set; } = null!;

	/// <summary>
	/// Gets or sets the fully qualified message type name.
	/// </summary>
	public string MessageTypeFullName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the simple message type name.
	/// </summary>
	public string MessageTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message kind (Command, Query, DomainEvent, IntegrationEvent, Message).
	/// </summary>
	public string MessageKind { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether this pipeline is deterministic
	/// and suitable for static generation.
	/// </summary>
	public bool IsDeterministic { get; set; }

	/// <summary>
	/// Gets or sets the reason if the pipeline is non-deterministic.
	/// </summary>
	public string? NonDeterministicReason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message returns a result.
	/// </summary>
	public bool HasResult { get; set; }

	/// <summary>
	/// Gets or sets the result type if the message returns one.
	/// </summary>
	public ITypeSymbol? ResultType { get; set; }

	/// <summary>
	/// Gets or sets the fully qualified result type name.
	/// </summary>
	public string? ResultTypeFullName { get; set; }

	/// <summary>
	/// Gets or sets the interceptable location data from Roslyn for intercepting DispatchAsync calls.
	/// </summary>
	public string? InterceptableLocationData { get; set; }

	/// <summary>
	/// Gets or sets the file path for unique identifier generation.
	/// </summary>
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the line number (1-based) for unique identifier generation.
	/// </summary>
	public int Line { get; set; }

	/// <summary>
	/// Gets or sets the column number (1-based) for unique identifier generation.
	/// </summary>
	public int Column { get; set; }

	/// <summary>
	/// Gets a unique identifier for the generated interceptor method name.
	/// </summary>
	public string UniqueId => $"Static_{SafeIdentifier}_{Line}_{Column}";

	/// <summary>
	/// Gets a safe identifier for the message type (for generated code).
	/// </summary>
	public string SafeIdentifier => MessageTypeName.Replace(".", "_").Replace("+", "_");

	/// <summary>
	/// Determines if two PipelineChainInfo instances represent the same call site.
	/// </summary>
	public bool Equals(PipelineChainInfo? other)
	{
		if (other is null)
		{
			return false;
		}

		return MessageTypeFullName == other.MessageTypeFullName &&
			   FilePath == other.FilePath &&
			   Line == other.Line &&
			   Column == other.Column;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as PipelineChainInfo);

	/// <inheritdoc />
	public override int GetHashCode()
	{
		unchecked
		{
			var hash = 17;
			hash = (hash * 31) + (MessageTypeFullName?.GetHashCode() ?? 0);
			hash = (hash * 31) + (FilePath?.GetHashCode() ?? 0);
			hash = (hash * 31) + Line;
			hash = (hash * 31) + Column;
			return hash;
		}
	}
}
