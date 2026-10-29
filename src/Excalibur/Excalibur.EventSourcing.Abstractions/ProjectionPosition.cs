// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

namespace Excalibur.EventSourcing;

/// <summary>The meaning of a projection row's stored position.</summary>
public enum ProjectionPositionKind
{
	/// <summary>The state is not a fold over a known prefix. This is the fail-safe default.</summary>
	Unplaceable,
	/// <summary>The state contains the fold through a measured global position.</summary>
	Positioned,
	/// <summary>The state is a complete fold, but its global coordinate is unknown.</summary>
	Unnumbered,
	/// <summary>The key is permanently deleted within its tenant and physical generation.</summary>
	Retired,
}

/// <summary>A projection's progress assertion or terminal retirement state.</summary>
/// <remarks>
/// Providers store state and position atomically. Existing unnumbered and unplaceable rows cannot
/// accept an ordinary positioned advance; a full rebuild can repair them. A retired key cannot be
/// repaired in place: its payload is gone and every writer must preserve the deletion marker.
/// Repopulation requires a new physical generation. An absent row has no assertion and reads as
/// Unplaceable; it is distinct from a retained retirement marker even though both expose no payload.
/// </remarks>
public readonly record struct ProjectionPosition
{
	/// <summary>Encoding of a complete fold without a global coordinate.</summary>
	public const long UnnumberedSentinel = -1L;
	/// <summary>Encoding of state that is not a fold over a known prefix.</summary>
	public const long UnplaceableSentinel = -2L;
	/// <summary>Encoding of terminal deletion within a physical generation.</summary>
	public const long RetiredSentinel = -3L;

	private readonly long _value;

	private ProjectionPosition(ProjectionPositionKind kind, long value)
	{
		Kind = kind;
		_value = value;
	}

	/// <summary>Gets the meaning of the stored position.</summary>
	public ProjectionPositionKind Kind { get; }

	/// <summary>Gets the global coordinate of a positioned row.</summary>
	/// <exception cref="InvalidOperationException">This state carries no global coordinate.</exception>
	public long Value => Kind == ProjectionPositionKind.Positioned
		? _value
		: throw new InvalidOperationException($"A projection position of kind '{Kind}' carries no number.");

	/// <summary>Gets a complete fold whose global coordinate is unknown.</summary>
	public static ProjectionPosition Unnumbered { get; } = new(ProjectionPositionKind.Unnumbered, UnnumberedSentinel);

	/// <summary>Gets state that cannot be related to a known event prefix.</summary>
	public static ProjectionPosition Unplaceable { get; } = new(ProjectionPositionKind.Unplaceable, UnplaceableSentinel);

	/// <summary>Gets a terminal deletion marker with no payload or global coordinate.</summary>
	public static ProjectionPosition Retired { get; } = new(ProjectionPositionKind.Retired, RetiredSentinel);

	/// <summary>Gets the observed global coordinate, or null for a state without one.</summary>
	/// <remarks>
	/// Null does not authorize insertion. The store checks row existence and retirement atomically
	/// with every write; a retired row refuses even a caller that read no position.
	/// </remarks>
	public long? ExpectedPositionOrNull => Kind == ProjectionPositionKind.Positioned ? _value : null;

	/// <summary>Creates an assertion that the state is folded through the given position.</summary>
	/// <param name="position">A non-negative global coordinate.</param>
	/// <returns>The positioned assertion.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The coordinate is negative.</exception>
	public static ProjectionPosition At(long position)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(position);
		return new ProjectionPosition(ProjectionPositionKind.Positioned, position);
	}

	/// <summary>Decodes the shared storage representation.</summary>
	/// <param name="stored">The stored number, or null when no assertion was recorded.</param>
	/// <returns>The explicit state; absent or unknown negative values are unplaceable.</returns>
	public static ProjectionPosition FromStored(long? stored) => stored switch
	{
		UnnumberedSentinel => Unnumbered,
		RetiredSentinel => Retired,
		null or < 0 => Unplaceable,
		_ => At(stored.Value),
	};

	/// <summary>Encodes the assertion, preserving the fail-safe default struct value.</summary>
	/// <returns>The coordinate or shared sentinel.</returns>
	public long ToStored() => Kind switch
	{
		ProjectionPositionKind.Positioned => _value,
		ProjectionPositionKind.Unnumbered => UnnumberedSentinel,
		ProjectionPositionKind.Retired => RetiredSentinel,
		_ => UnplaceableSentinel,
	};

	/// <summary>Returns the global coordinate or state name for diagnostics.</summary>
	/// <returns>A diagnostic string.</returns>
	public override string ToString() => Kind == ProjectionPositionKind.Positioned
		? _value.ToString(CultureInfo.InvariantCulture)
		: Kind.ToString();
}
