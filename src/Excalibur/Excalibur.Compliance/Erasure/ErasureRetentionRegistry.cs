// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// The aggregate types a deployment is legally obliged to keep through an erasure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared in code rather than bound from configuration, and the reason is measured rather than
/// assumed.</b> <c>required</c> is a compiler rule, not a runtime one: the reflection-based configuration
/// binder constructs through <c>Activator</c> and half-writes such a record without complaint. What
/// refuses it is the SOURCE-GENERATED binder, which this framework's consumers are on because it targets
/// trimming and ahead-of-time compilation — it fails their build outright on a required member it cannot
/// set. Declaring in code is therefore the only shape that keeps the compiler's guarantee. A host that
/// keeps the wording in its own configuration still reads it from there and passes it in.
/// </para>
/// <para>
/// Internal because its only mutations that mean anything happen before the host starts. The declarations
/// are snapshotted once, so a list handed to a consumer could be added to after startup with no effect
/// and no error — and the failure mode of a retention that did not take effect is a legally-required
/// record destroyed. Declare through <c>AddErasureRetention</c> instead.
/// </para>
/// </remarks>
internal sealed class AggregateRetentionOptions
{
	/// <summary>
	/// Gets the declared retentions.
	/// </summary>
	/// <value>The aggregate types that must survive an erasure, with the obligation behind each.</value>
	public IList<ErasureRetention> Aggregates { get; } = [];
}

/// <summary>
/// Answers the retention question from the declarations on <see cref="AggregateRetentionOptions.Aggregates"/>.
/// </summary>
/// <remarks>
/// A snapshot taken once at construction. Retention states the law the deployment operates under, not a
/// runtime signal, and a set that could change mid-erasure would let one erasure destroy one aggregate and
/// keep its sibling for reasons nothing recorded.
/// </remarks>
internal sealed class ErasureRetentionRegistry : IErasureRetentionRegistry
{
	private readonly FrozenDictionary<RetentionKey, ErasureRetention> _byType;
	private readonly ErasureRetention[] _declared;

	// The aggregate types SOMEONE declared, with the tenant projected away. This is the write path's whole
	// question, and holding it as its own set is what keeps that path off a scan of every declaration -- it
	// runs on every personal-data write into an aggregate, not on a rare branch.
	private readonly FrozenSet<string> _retainedTypes;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureRetentionRegistry"/> class.
	/// </summary>
	/// <param name="options">The declared aggregate retentions.</param>
	public ErasureRetentionRegistry(IOptions<AggregateRetentionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Grouping rather than ToFrozenDictionary on the sequence directly, so construction is TOTAL. The
		// validator rejects a duplicate (tenant, type), so a duplicate cannot reach a validated options
		// instance -- but an options instance built by hand still must not make a registry lookup throw in
		// the middle of an erasure.
		//
		// The tenant is NORMALISED into the key, not stored as declared, so the lookup cannot miss on a
		// spelling: a declaration naming the sentinel, an empty string or whitespace all key the same way,
		// which is the same way an erasure request naming no tenant is keyed on the read below.
		_byType = options.Value.Aggregates
			.Where(static r => r is not null && !string.IsNullOrWhiteSpace(r.AggregateType))
			.GroupBy(static r => new RetentionKey(NormaliseTenant(r.TenantId), r.AggregateType))
			.ToFrozenDictionary(static g => g.Key, static g => g.First());

		_declared = [.. _byType.Values];
		_retainedTypes = _byType.Keys
			.Select(static k => k.AggregateType)
			.ToFrozenSet(StringComparer.Ordinal);
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<ErasureRetention> Declared => _declared;

	/// <inheritdoc/>
	public bool IsRetainedForAnyTenant(string? aggregateType) =>
		!string.IsNullOrWhiteSpace(aggregateType) && _retainedTypes.Contains(aggregateType);

	/// <inheritdoc/>
	public bool TryGetRetention(
		string? tenantId,
		string? aggregateType,
		[NotNullWhen(true)] out ErasureRetention? retention)
	{
		// TOTAL, and deliberately so. This is consulted on the path that decides whether to destroy a
		// record; throwing here would convert an unknown type into a failed erasure rather than an erased
		// one, and a Try that throws is not the contract its name advertises.
		if (string.IsNullOrWhiteSpace(aggregateType))
		{
			retention = null;

			return false;
		}

		return _byType.TryGetValue(new RetentionKey(NormaliseTenant(tenantId), aggregateType), out retention);
	}

	/// <summary>
	/// The tenant term a declaration is keyed on, and the ONLY derivation of it.
	/// </summary>
	/// <param name="tenantId">The tenant as declared or as an erasure recorded it.</param>
	/// <returns>The normalised term: every spelling of <em>untenanted</em> collapses onto the sentinel.</returns>
	/// <remarks>
	/// <para>
	/// The spelling rule is the framework's own rather than a local one. <c>FromStoredValue</c> collapses
	/// absent, empty, whitespace and the sentinel itself onto the sentinel, so a declaration and an erasure
	/// request meaning the same tenant compare equal however each was written. A local "== sentinel" test
	/// would be a second predicate that silently disagrees about empty and whitespace.
	/// </para>
	/// <para>
	/// <b>Internal, and shared with the startup validator on purpose.</b> The validator's duplicate-scope
	/// check and this lookup are two predicates over one concept, and they MUST derive the key the same
	/// way. While the validator keyed on the tenant as written, two declarations whose tenants normalised
	/// together passed its duplicate rule and then collapsed here — first-declared wins, the second
	/// silently discarded, so the basis and period on a signed erasure record were decided by the order two
	/// lines appear in a registration call. Sharing the derivation makes that disagreement inexpressible
	/// rather than merely caught by whatever the validator happens to reject today.
	/// </para>
	/// </remarks>
	internal static string NormaliseTenant(string? tenantId) =>
		KeyedTenantPartition.FromStoredValue(tenantId).TenantId;

	/// <summary>The lookup key: one aggregate type, in one tenant.</summary>
	/// <param name="TenantId">The normalised tenant term, never null.</param>
	/// <param name="AggregateType">The aggregate type, matched ordinally.</param>
	private readonly record struct RetentionKey(string TenantId, string AggregateType);
}

/// <summary>
/// The retained aggregate type's contribution to the key handle protecting one data subject's personal
/// fields inside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It composes nothing.</b> Composing a retained handle lives on <see cref="SubjectKeyHandle.Retained"/>,
/// which can only be reached from a handle that already carries a tenant. This type used to own a
/// <c>For(string subjectKeyHandle, string aggregateType)</c> overload, and a bare-string handle parameter is
/// what let the erasure path compose a retained handle out of a raw pseudonymisation token carrying no
/// tenant — the cross-tenant key collision. With the composition moved, that call does not compile.
/// </para>
/// <para>
/// <b>A handle is a NAME, so this needs no new cryptography.</b> The key provider mints random material at
/// whatever handle it is asked for, so widening the handle with the aggregate type yields a second,
/// independently destroyable key. Nothing is derived from anything, and the subject's own key is not a
/// parent of this one.
/// </para>
/// <para>
/// <b>The composition is a digest rather than a delimiter, because the handle is a key-store identifier.</b>
/// Key-management backends constrain the character set and the length of a key name — Azure Key Vault
/// accepts alphanumerics and hyphens up to 127 characters — and an aggregate type is a consumer-chosen
/// string that may carry dots or exceed what is left. Hashing the type to sixteen hexadecimal characters
/// keeps every handle in the shape the subject handle already has, which is the shape those backends are
/// already known to accept.
/// </para>
/// <para>
/// <b>Truncation is safe here because the population is declared and checked.</b> Sixteen hexadecimal
/// characters is sixty-four bits, and the only inputs are the aggregate types a deployment declares a
/// retention for — a set known at startup, where validation rejects two types whose discriminators collide.
/// The map from declared type to handle is therefore verified injective for the deployment, rather than
/// assumed injective in general.
/// </para>
/// </remarks>
internal static class RetainedKeyHandle
{
	/// <summary>
	/// The aggregate type's contribution to the handle, alone, so startup validation can check that two
	/// declared types do not produce the same one.
	/// </summary>
	/// <param name="aggregateType">The retained aggregate type.</param>
	/// <returns>Sixteen uppercase hexadecimal characters.</returns>
	internal static string DiscriminatorFor(string aggregateType)
	{
		Span<byte> digest = stackalloc byte[32];
		_ = SHA256.HashData(Encoding.UTF8.GetBytes(aggregateType), digest);
		return Convert.ToHexString(digest[..8]);
	}
}
