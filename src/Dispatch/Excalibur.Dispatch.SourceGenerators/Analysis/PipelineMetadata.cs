// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Analysis;

/// <summary>
/// Contains metadata about a message type's middleware pipeline determinism.
/// </summary>
/// <remarks>
/// This metadata enables full static pipeline generation
/// by identifying which message types have deterministic (non-conditional) middleware pipelines.
/// </remarks>
internal sealed class PipelineMetadata
{
	/// <summary>
	/// Gets or sets the message type symbol.
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
	/// Gets or sets a value indicating whether the pipeline is deterministic.
	/// </summary>
	/// <remarks>
	/// A pipeline is deterministic if:
	/// <list type="bullet">
	/// <item>All middleware types are statically known at compile time</item>
	/// <item>No conditional middleware registration (feature flags, if statements)</item>
	/// <item>No tenant-specific middleware</item>
	/// <item>Middleware ordering is fixed</item>
	/// </list>
	/// </remarks>
	public bool IsDeterministic { get; set; }

	/// <summary>
	/// Gets or sets the reason why the pipeline is non-deterministic.
	/// </summary>
	public string? NonDeterministicReason { get; set; }

	/// <summary>
	/// Gets or sets the message kind (Command, Query, Event, Notification).
	/// </summary>
	public string MessageKind { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the list of middleware types that apply to this message type.
	/// </summary>
	public List<string> ApplicableMiddleware { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the message has custom pipeline profile attributes.
	/// </summary>
	public bool HasCustomPipelineProfile { get; set; }

	/// <summary>
	/// Gets or sets the pipeline profile name if specified via attribute.
	/// </summary>
	public string? PipelineProfileName { get; set; }

	/// <summary>
	/// Gets a safe identifier for the message type (for generated code).
	/// </summary>
	public string SafeIdentifier => MessageTypeName.Replace(".", "_").Replace("+", "_");

	/// <summary>
	/// Determines if two PipelineMetadata instances represent the same message type.
	/// </summary>
	public bool Equals(PipelineMetadata? other)
	{
		if (other is null)
		{
			return false;
		}

		return MessageTypeFullName == other.MessageTypeFullName;
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as PipelineMetadata);

	/// <inheritdoc />
	public override int GetHashCode() => MessageTypeFullName.GetHashCode();
}
