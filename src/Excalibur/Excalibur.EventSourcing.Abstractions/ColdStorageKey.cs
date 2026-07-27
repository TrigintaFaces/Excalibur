// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Text;
using System.Text;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Composes the tenant-qualified segment of a cold-storage object key.
/// </summary>
/// <remarks>
/// <para>
/// Cold stores address objects by a flat string key, so a tenant and an aggregate identifier must be
/// combined into one path. Naive concatenation with a delimiter makes distinct pairs collide whenever a
/// component can contain the delimiter (or the character a provider substitutes for it): tenant <c>"t/a"</c>
/// with aggregate <c>"b"</c> and tenant <c>"t"</c> with aggregate <c>"a/b"</c> both render as
/// <c>"t/a/b"</c>. A collision here is a cross-tenant read.
/// </para>
/// <para>
/// This helper encodes the tenant identifier with Base64Url, which is <em>injective</em>: distinct tenants
/// always produce distinct segments, and the alphabet (<c>A-Z a-z 0-9 - _</c>) contains no path separator,
/// so the segment cannot introduce structure into the key. Collisions become inexpressible rather than
/// sanitized away, and each provider keeps its own idiom for the remainder of the key.
/// </para>
/// <para>
/// A lossy substitution (replacing path separators with a placeholder) is <strong>not</strong> sufficient
/// and must not be reintroduced: it maps distinct tenants onto the same segment, and every tenant that
/// collides shares one object. Length-prefixing does not repair that — equal-length inputs collide
/// identically. Only a reversible encoding makes the guarantee.
/// </para>
/// <para>
/// <strong>This encoding is a persisted wire format, not an implementation detail.</strong> The value it
/// produces becomes part of the object key under which events are durably stored, so changing it orphans
/// every object already written: existing archives remain in cold storage under their old keys, and reads
/// composed with the new encoding return nothing — indistinguishable, to a caller, from an aggregate that
/// was never archived. Treat any change as a data migration requiring a re-key of existing objects, never
/// as a refactor. Callers MUST NOT parse or construct these segments themselves.
/// </para>
/// </remarks>
public static class ColdStorageKey
{
	/// <summary>
	/// Builds the tenant-qualified prefix segment for a cold-storage key.
	/// </summary>
	/// <param name="tenant">The tenant partition that owns the object.</param>
	/// <returns>
	/// A path segment uniquely determined by <paramref name="tenant"/>, safe to concatenate with an
	/// aggregate identifier without ambiguity.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="tenant"/> is <see langword="null"/>.</exception>
	public static string TenantSegment(KeyedTenantPartition tenant)
	{
		ArgumentNullException.ThrowIfNull(tenant);

		// Base64Url is injective and its alphabet excludes '/' and '\', so two distinct tenants can never
		// share a segment and a tenant identifier can never inject a path boundary into the key.
		return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(tenant.TenantId));
	}

	/// <summary>
	/// Builds the aggregate segment for a cold-storage key.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier the object holds events for.</param>
	/// <returns>
	/// A path segment uniquely determined by <paramref name="aggregateId"/>, safe to concatenate with a
	/// tenant segment without ambiguity.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="aggregateId"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// <para>
	/// Encoding the aggregate term is <strong>not</strong> redundant beside an injective tenant segment. A
	/// key is a function of <em>both</em> components, so it is injective only if both are: sanitizing the
	/// aggregate identifier by rewriting unsafe characters — <c>Replace('/', '_')</c>,
	/// <c>Replace('\\', '_')</c> — is many-to-one, and maps the distinct identifiers <c>a/b</c>, <c>a\b</c>
	/// and <c>a_b</c> onto the <em>same</em> object within a single tenant. Two aggregates then share one
	/// object: each read returns the other's events and each write silently overwrites them.
	/// </para>
	/// <para>
	/// This is the same defect as the tenant-segment collision in a narrower form — one tenant's aggregates
	/// rather than several tenants — so it is closed the same way, by a reversible encoding rather than a
	/// sanitation map. The alphabet excludes <c>/</c> and <c>\</c>, so an aggregate identifier can neither
	/// alias a sibling nor inject a path boundary into the key.
	/// </para>
	/// <para>
	/// <strong>Persisted wire format.</strong> The same migration constraint stated for
	/// <see cref="TenantSegment"/> applies here verbatim: changing this encoding orphans every object
	/// already written under it.
	/// </para>
	/// </remarks>
	public static string AggregateSegment(string aggregateId)
	{
		ArgumentNullException.ThrowIfNull(aggregateId);

		return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(aggregateId));
	}
}
