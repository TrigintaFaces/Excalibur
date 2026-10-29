// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>
/// One projection bound to a subscription: its stable key, the fingerprint of its processing
/// definition, and the destination that fingerprint was computed over.
/// </summary>
/// <param name="ProjectionKey">
/// The projection's stable identity within the subscription. It is one half of the key its member row
/// and its checkpoint are both stored under; the subscription name is the other half.
/// </param>
/// <param name="DefinitionHash">
/// An uppercase 64-character hexadecimal SHA-256 fingerprint over this projection's processing
/// definition: the subscription's source/destination scope, its consumer semantic revision, and this
/// projection's assembly, type identity and destination. The hash and destination must both match an existing binding.
/// </param>
/// <param name="Destination">
/// The resolved destination identity (index, table or container) the fingerprint was computed over.
/// Compared when joining a binding and used as the authoritative destination for generation cutover.
/// </param>
public sealed record SubscriptionMember(string ProjectionKey, string DefinitionHash, string Destination)
{
    /// <summary>Gets the physical storage scope captured for this binding's lifetime.</summary>
    /// <value>The provider's verified service/database identity, or null when unavailable.</value>
    public string? DestinationScope { get; init; }

	/// <summary>Gets the store-assigned identity of this binding's lifetime.</summary>
	/// <value>Empty on a binding request; a nonempty identifier on a stored member.</value>
	public Guid BindingId { get; init; }

	/// <summary>Gets the checkpoint read atomically with this member.</summary>
	/// <value>Null until this binding has acknowledged a position.</value>
	public long? Position { get; init; }
}

/// <summary>
/// Every projection a subscription is bound to, as the durable store holds it.
/// </summary>
/// <param name="SubscriptionName">The stable subscription name the roster belongs to.</param>
/// <param name="Members">
/// Every bound member, INCLUDING any the caller did not supply. That inclusion is the point of this
/// type: a member present in the store and absent from the calling process is work this process cannot
/// perform and therefore cannot attest, and it can only be accounted for if the caller is told it
/// exists.
/// </param>
/// <remarks>
/// <para>
/// THE ROSTER IS A PROPERTY OF THE SUBSCRIPTION, NOT OF THE PROCESS, and that is the whole reason this
/// type is returned rather than the binding simply succeeding or throwing. Any attestation quantified
/// over "every registration" -- the freshness a returning save reports -- is honest only while the
/// quantifier's domain is read from the durable store. Computed instead from the local registry, the
/// attestation narrows silently to whatever this process happens to host: a save folds the projections
/// it can see, reports fresh, and says nothing about a sibling bound by another deployment that has not
/// folded the same events. Nothing downstream can detect that, because the caller was told it need not
/// re-check.
/// </para>
/// </remarks>
public sealed record SubscriptionRoster(string SubscriptionName, IReadOnlyList<SubscriptionMember> Members)
{
	/// <summary>Gets a fingerprint of the roster's member lifetimes, independent of their progress.</summary>
	/// <value>A SHA-256 digest of the sorted keys and binding identifiers.</value>
	public string Identity
	{
		get
		{
			var text = new StringBuilder();
			foreach (var member in Members.OrderBy(static m => m.ProjectionKey, StringComparer.Ordinal))
			{
				text.Append(member.ProjectionKey.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
					.Append(member.ProjectionKey).Append(member.BindingId.ToString("N"));
			}
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
		}
	}
}

/// <summary>
/// Composes the durable keys a subscription uses. The single source of truth for those strings.
/// </summary>
/// <remarks>
/// Shared rather than private because more than one party must derive the SAME strings, and a second
/// copy of either expression is a silent failure waiting to happen.
/// </remarks>
public static class SubscriptionKeys
{
	/// <summary>Gets the durable member key for a projection's declared generation.</summary>
	/// <param name="projectionType">The read-model type.</param>
	/// <returns>The type identity qualified by its declared generation.</returns>
	public static string ProjectionKey(Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		return ProjectionVersionConvention.Qualify(projectionType.FullName ?? projectionType.Name, projectionType, ':');
	}

	/// <summary>Gets a member key for an explicitly selected generation, including a previous generation.</summary>
	/// <param name="projectionType">The read-model type whose identity is used.</param>
	/// <param name="version">The generation, starting at one.</param>
	/// <returns>The durable member key.</returns>
	public static string ProjectionKey(Type projectionType, int version)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
		var name = projectionType.FullName ?? projectionType.Name;
		return version == 1 ? name : string.Concat(name, ":v", version.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>One projection's in-process identity within a subscription.</summary>
	/// <param name="subscriptionName">The stable subscription name.</param>
	/// <param name="projectionKey">The projection's key, as carried by <see cref="SubscriptionMember"/>.</param>
	/// <returns>A single string naming that projection's cursor, for in-process bookkeeping.</returns>
	/// <remarks>
	/// <para>
	/// THIS IS NOT A STORAGE KEY, and it must never become one again. A durable cursor is addressed by
	/// the PAIR -- the subscription name and the projection key, in their own columns, bounded at 255 and
	/// 512 -- because the composition reaches 768 while any one column wide enough for the name alone is
	/// too narrow for both. A deeply namespaced projection type reaches that, and the member row binds
	/// successfully (its own columns are 255 and 512, and both inputs are individually legal), so the
	/// result is a bound member whose cursor can never be written: a permanent stall, not a refusal. On a
	/// provider configured to truncate rather than refuse, it is worse -- two projections share one
	/// cursor and each skips what the other consumed.
	/// </para>
	/// <para>
	/// What it is still for: keying the in-process progress map a single host keeps, where one string per
	/// projection is the natural dictionary key and no column bounds it. Both parts are compared
	/// ordinally there, as they are in storage.
	/// </para>
	/// </remarks>
	public static string CheckpointName(string subscriptionName, string projectionKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionKey);

		return $"{subscriptionName}:{projectionKey}";
	}

	/// <summary>
	/// Computes one member's definition fingerprint. The single source of truth for that computation.
	/// </summary>
	/// <param name="subscriptionScope">The subscription's logical source and destination scope.</param>
	/// <param name="subscriptionRevision">The consumer's semantic revision.</param>
	/// <param name="assemblyName">The projection type's assembly name.</param>
	/// <param name="projectionKey">The projection type's identity, as carried by a member.</param>
	/// <param name="destination">The resolved destination identity, or empty when none is advertised.</param>
	/// <returns>An uppercase 64-character hexadecimal SHA-256 fingerprint.</returns>
	/// <remarks>
	/// <para>
	/// SHARED BECAUSE MORE THAN ONE HOST COMPUTES IT, and a second copy would be worse than a second
	/// copy of a key. Two hosts that encode the same definition differently produce different
	/// fingerprints for identical configuration, so each refuses the other's binding permanently, and the
	/// refusal names a projection whose definition a reader can see is the same. A drifting key makes a
	/// cursor unreachable; a drifting fingerprint makes the subscription unstartable.
	/// </para>
	/// <para>
	/// Each part is LENGTH-PREFIXED rather than delimited, which is what keeps the encoding unambiguous
	/// when a part contains the separator. Without it, two different definitions whose parts concatenate
	/// to the same string would collide and silently share a binding.
	/// </para>
	/// </remarks>
	public static string DefinitionFingerprint(
		string subscriptionScope,
		string subscriptionRevision,
		string assemblyName,
		string projectionKey,
		string destination)
	{
		ArgumentNullException.ThrowIfNull(subscriptionScope);
		ArgumentNullException.ThrowIfNull(subscriptionRevision);
		ArgumentNullException.ThrowIfNull(assemblyName);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionKey);
		ArgumentNullException.ThrowIfNull(destination);

		var descriptor = new StringBuilder();
		foreach (var part in (string[])[subscriptionScope, subscriptionRevision, assemblyName, projectionKey, destination])
		{
			_ = descriptor
				.Append(part.Length.ToString(CultureInfo.InvariantCulture))
				.Append(':')
				.Append(part);
		}

		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(descriptor.ToString())));
	}
}
