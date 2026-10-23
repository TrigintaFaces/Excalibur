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
			// Tenant is checked alongside AggregateType because CONSTRUCTION IS TOTAL BY CONTRACT and
			// r.Tenant.TenantId below would throw on a hand-built options instance whose partition is
			// absent. The startup validator refuses such a declaration; this keeps the registry from
			// throwing before the validator can report it, which would turn a clear startup failure into
			// a container-build stack trace.
			.Where(static r => r is not null && r.Tenant is not null
				&& !string.IsNullOrWhiteSpace(r.AggregateType))
			.GroupBy(static r => new RetentionKey(r.Tenant.TenantId, r.AggregateType))
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
		KeyedTenantPartition tenant,
		string? aggregateType,
		[NotNullWhen(true)] out ErasureRetention? retention)
	{
		ArgumentNullException.ThrowIfNull(tenant);

		// TOTAL, and deliberately so. This is consulted on the path that decides whether to destroy a
		// record; throwing here would convert an unknown type into a failed erasure rather than an erased
		// one, and a Try that throws is not the contract its name advertises.
		if (string.IsNullOrWhiteSpace(aggregateType))
		{
			retention = null;

			return false;
		}

		return _byType.TryGetValue(new RetentionKey(tenant.TenantId, aggregateType), out retention);
	}

	/// <summary>
	/// The key a declaration and a request are joined on: one aggregate type, in one tenant partition.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>There is no normaliser here any more, and its absence is the fix.</b> Both operands are now
	/// partitions — a declaration states <see cref="ErasureRetention.Tenant"/> and a request carries
	/// <see cref="ErasureStatus.TenantPartition"/> — and <see cref="KeyedTenantPartition"/> has exactly one
	/// inhabitant per partition, so there is no second spelling for a normaliser to reconcile.
	/// </para>
	/// <para>
	/// <b>What stood here, and why it had to go rather than be kept as defence in depth.</b> The tenant was
	/// a <see langword="string"/> on both sides, and a single-tenant deployment declared the untenanted
	/// sentinel while its requests were stamped with the ambient single-tenant identity — a different
	/// reserved term. The join missed, and a miss on this path destroys the key protecting a legally
	/// retained record while the signed certificate reports it as kept. The repair was a derivation that
	/// mapped one term onto the other, which worked and left two problems: it depended on the ambient
	/// default being unnameable by a real tenant, which <see cref="TenantDefaults.DefaultTenantId"/>
	/// explicitly denies, and it kept a conflation alive behind a function that made it look settled.
	/// Deciding the partition where the request is FILED, from whether multi-tenancy is configured rather
	/// than from the shape of a string, removes both.
	/// </para>
	/// </remarks>

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
