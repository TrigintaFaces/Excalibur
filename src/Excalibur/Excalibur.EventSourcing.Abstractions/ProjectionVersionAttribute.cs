// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Declares the generation of a projection's read model, so that changing how the model is shaped or
/// folded sends it to a new destination instead of mutating the one already serving reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this exists to prevent.</b> Renaming a property or changing a fold rule alters what the
/// stored documents mean while leaving the destination -- the index, container, collection or table --
/// addressed by exactly the same name. The old and new shapes then interleave in one destination, and
/// nothing reports it: a search provider may reject the write on a mapping conflict, but the remaining
/// providers accept it and the read model is silently part stale. Declaring a new generation makes the
/// destination name different, so the new generation folds from zero into its own destination while the
/// previous one keeps its cursor and keeps serving reads until you cut over.
/// </para>
/// <para>
/// <b>Generation 1 is the unversioned destination, byte for byte.</b> A projection with no attribute is
/// generation 1, and so is one declaring <c>1</c> explicitly: both resolve to the destination name they
/// resolve to today. A version segment appears only from generation 2 onward. That is what lets an
/// existing read model adopt this attribute without re-folding -- label the current shape
/// <c>[ProjectionVersion(1)]</c> and nothing moves; the first bump to <c>2</c> is the first new
/// destination.
/// </para>
/// <para>
/// <b>It is read once, when the store is constructed.</b> The value is a compile-time constant of the
/// read-model type rather than a configuration setting, so it cannot change under a running store whose
/// destination was already resolved, and a tool that plans a cutover can read it from the type without
/// building a container.
/// </para>
/// <para>
/// <b>Where the segment lands is the provider's choice, not a shared format.</b> Each provider places it
/// using the separator its own identifiers allow, because a name that is legal for a search index is not
/// always a legal SQL identifier.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Generation 2 folds into its own destination; generation 1 keeps serving reads meanwhile.
/// [ProjectionVersion(2)]
/// public sealed class OrderSummary
/// {
///     public string OrderId { get; set; } = string.Empty;
///     public string CustomerName { get; set; } = string.Empty; // renamed from Customer in generation 2
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class ProjectionVersionAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionVersionAttribute"/> class.
	/// </summary>
	/// <param name="version">
	/// The generation of the read model, from 1. Generation 1 resolves to the same destination name as a
	/// projection that declares no generation at all.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="version"/> is less than 1.
	/// </exception>
	public ProjectionVersionAttribute(int version)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
		Version = version;
	}

	/// <summary>
	/// Gets the declared generation of the read model.
	/// </summary>
	/// <value>The generation number, from 1.</value>
	public int Version { get; }
}
