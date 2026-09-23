// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>
/// Provides methods to generate cache keys for authorization-related data.
/// </summary>
/// <remarks>
/// <para>
/// These keys are a pure function of their inputs. They carry no application or tenant prefix, and they
/// deliberately do not read one from configuration: a key builder is the wrong place to decide which
/// application a cache entry belongs to.
/// </para>
/// <para>
/// <b>The keys are therefore NOT self-scoping.</b> Two applications sharing one distributed cache would
/// address the same entry for the same user, so the cache registration is responsible for scoping the
/// keyspace. What these keys guarantee is only that a given user's grants and the activity-group set have
/// stable, distinct names within whatever keyspace they are placed in.
/// </para>
/// </remarks>
public static class AuthorizationCacheKey
{
	/// <summary>
	/// Generates a cache key for authorization grants for a specific user.
	/// </summary>
	/// <param name="userId"> The unique identifier of the user. </param>
	/// <returns> A string representing the cache key for user grants. </returns>
	public static string ForGrants(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		return $"authorization/{userId}/grants";
	}

	/// <summary>
	/// Generates a cache key for storing one tenant's activity group data.
	/// </summary>
	/// <param name="tenantId"> The tenant whose activity groups the entry holds. </param>
	/// <returns> A string representing the cache key for that tenant's activity groups. </returns>
	/// <remarks>
	/// <para>
	/// <b>The key carries the SHAPE VERSION of the document it names, and it must be incremented whenever
	/// that shape changes.</b> The cached activity-group document is read back into a declared type; two
	/// application instances running different versions of this library share one distributed cache, so a
	/// constant key would let an instance read a document written in a shape it cannot interpret.
	/// </para>
	/// <para>
	/// The failure is bidirectional during a rolling deploy — the old instance writes the old shape and the
	/// new instance reads it, or the reverse — and it takes <b>either of two forms, neither of which is
	/// acceptable on an authorization path</b>:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// <b>A hard failure.</b> A document whose values cannot be read as the declared shape raises a
	/// deserialization error, and nothing on this path catches it, so it propagates out of the policy
	/// provider. Every authorization decision against that entry fails outright while both versions are
	/// live.
	/// </description></item>
	/// <item><description>
	/// <b>A silent denial.</b> A document that does parse into a shape carrying no usable members yields
	/// no activity groups, and <b>every activity-group grant is denied</b> instead. A denial is
	/// indistinguishable from a policy decision, so nothing surfaces it: no exception, no log, and the
	/// deploy completes looking healthy.
	/// </description></item>
	/// </list>
	/// <para>
	/// Which of the two occurs depends on the shapes involved, so neither can be relied on as the warning
	/// the other lacks. Versioning removes both.
	/// </para>
	/// <para>
	/// Versioning the key makes the two shapes address different entries, so neither instance can read the
	/// other's document and each simply misses and reloads from the store. The superseded entry is then
	/// touched only by the instances that understand it, and expires on its own sliding expiration once
	/// they are gone; nothing has to clear it.
	/// </para>
	/// <para>
	/// <b>Waiting out the expiry is NOT an alternative remedy, and that is the reason the key must carry
	/// the version rather than be left alone.</b> The entry expires on a SLIDING window, so under a single
	/// shared key both versions keep reading it, each read renews it, and the window never lapses while
	/// either is live. The broken state would therefore persist for as long as the deployment takes and
	/// beyond it, rather than clearing itself after the configured duration.
	/// </para>
	/// <para>
	/// <b>The tenant segment is a correctness term, not a nicety.</b> The document holds ONE tenant's
	/// groups, because the store answers only for the tenant asked. A key that omitted the tenant would
	/// address one entry for the whole estate, so whichever tenant loaded it first would serve its document
	/// to every other — and a tenant reading a document composed for a different one finds none of its own
	/// groups under their composed keys, so <b>every activity-group grant is denied</b> with no exception
	/// and no log. The failure is silent in exactly the way a denial always is.
	/// </para>
	/// <para>
	/// <b>v3</b> — the document holds a single tenant's groups, keyed by composed (tenant, name).
	/// <b>v2</b> held every tenant's groups in one entry. <b>v1</b> declared its values as
	/// <see langword="object" />, which a JSON deserializer materialises as an element type satisfying no
	/// collection test.
	/// </para>
	/// </remarks>
	public static string ForActivityGroups(string tenantId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		return $"authorization/{tenantId}/activity-groups/v3";
	}
}
