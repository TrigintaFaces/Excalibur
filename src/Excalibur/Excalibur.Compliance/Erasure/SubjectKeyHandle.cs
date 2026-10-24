// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// The name of the key that protects one data subject's personal fields inside one tenant: the single
/// derivation both the write path and the erasure use, and the only way to obtain one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a type and not a <see cref="string"/>.</b> The handle this replaced was
/// <c>H_pepper(subjectId)</c> — CONSTANT IN TENANT. Data-subject identifiers are consumer-supplied from
/// their own entities, so customer numbers, employee identifiers and e-mail addresses repeat across
/// tenants in ordinary multi-tenant deployments. Two tenants whose identifiers collided therefore shared
/// ONE encryption key, and either tenant's erasure destroyed it for both: the victim's reads then returned
/// a tombstone — this framework's assertion of a lawful erasure — over data that was never erased and was
/// no longer recoverable. Nothing downstream learned, and nothing could.
/// </para>
/// <para>
/// The handle is the one identity in this subsystem with NO ROW BESIDE IT. Every other pseudonymised
/// identifier is a column on a record whose query carries a tenant term; a handle is a name in a key
/// vault, with nowhere to put a tenant column. So the name itself must carry the tenant, and injectivity
/// in the tenant is the whole requirement. That is also why
/// <see cref="IDataSubjectHasher.HashDataSubjectId"/> is deliberately NOT widened: changing record
/// pseudonymisation would break code that is already correct and would not fix this.
/// </para>
/// <para>
/// <b>The tenant is an EXPLICIT PARAMETER and is never read from ambient state in here.</b> The two
/// callers have two different authoritative tenants: the write path's is the ambient tenant of the request
/// doing the write; the erasure's is the tenant its own REQUEST recorded. An erasure runs from a
/// background processor whose ambient tenant is usually nothing and is never authoritative for a request
/// somebody else filed earlier, so a derivation that read ambient state would silently derive the wrong
/// handle there — and a wrong handle on the erasure path means destroying nothing while reporting a
/// completed erasure, which is the mirror image of the defect above and just as silent.
/// </para>
/// <para>
/// <b>Every caller supplies an exact resolved owner</b>, through <see cref="OwningTenant"/>, never
/// inside the derivation. The derivation cannot accept an absent tenant at all, which is what makes the
/// omission inexpressible rather than defaulted: a default buried inside a derivation is how "we forgot
/// the tenant" becomes "we silently used the wrong one".
/// </para>
/// <para>
/// Opaque: compare for equality, never for order, and do not read the characters for meaning. The value is
/// 64 uppercase hexadecimal characters — the same shape and width as the handle it replaced, so nothing
/// downstream of the derivation moves, and legal in every key-vault object-name charset this framework can
/// reach (a visible <c>tenantId:</c> prefix would not be: Key Vault object names are case-insensitive and
/// restricted to <c>0-9 a-z A-Z -</c>, so two tenants differing only in case would become one object —
/// the same collision, relocated).
/// </para>
/// </remarks>
internal readonly struct SubjectKeyHandle : IEquatable<SubjectKeyHandle>
{
	private readonly string? _value;

	private SubjectKeyHandle(string value) => _value = value;

	/// <summary>
	/// Gets the characters a key store resolves and an erasure destroys.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown for a <see langword="default"/> instance. A struct cannot forbid <see langword="default"/>, so
	/// this is the one bad state the type cannot make unrepresentable — and it is made LOUD rather than
	/// yielded as an empty string, because an empty handle would be queued for destruction and accepted by
	/// every store that takes a handle as a string.
	/// </exception>
	internal string Value => _value ?? throw new InvalidOperationException(
		"This is a default SubjectKeyHandle, which names no key. A handle is obtained from "
		+ $"{nameof(ForSubject)} or {nameof(ForSubjectHash)}, both of which require a tenant.");

	/// <summary>Preserves the caller's exact resolved owner for key derivation.</summary>
	/// <param name="tenantId">The write's captured owner or the erasure request's persisted owner.</param>
	/// <returns>The resolved owner, without normalization or fallback.</returns>
	/// <exception cref="ArgumentException">The owner is absent, blank or exceeds the supported length.</exception>
	/// <remarks>The explicit default tenant and the untenanted storage partition are distinct owners.</remarks>
	internal static TenantId OwningTenant(string? tenantId) => new(tenantId!);
	/// <summary>
	/// Derives the handle from a RAW data-subject identifier — the write path, which holds one.
	/// </summary>
	/// <param name="tenant">The tenant that owns the data being protected.</param>
	/// <param name="subjectId">The raw data-subject identifier.</param>
	/// <param name="hasher">The registered keyed hasher, which holds the deployment's pepper.</param>
	/// <returns>The handle of the key protecting that subject's fields in that tenant.</returns>
	internal static SubjectKeyHandle ForSubject(TenantId tenant, string subjectId, IDataSubjectHasher hasher)
	{
		ArgumentNullException.ThrowIfNull(hasher);
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

		return ForSubjectHash(tenant, hasher.HashDataSubjectId(subjectId), hasher);
	}

	/// <summary>
	/// Derives the handle from the data subject's PSEUDONYMISED identifier — the erasure path, which holds
	/// only that.
	/// </summary>
	/// <param name="tenant">The tenant that owns the data being erased.</param>
	/// <param name="dataSubjectIdHash">
	/// The pseudonymisation token <see cref="IDataSubjectHasher.HashDataSubjectId"/> produces, which is what
	/// an erasure request records.
	/// </param>
	/// <param name="hasher">The registered keyed hasher, which holds the deployment's pepper.</param>
	/// <returns>The handle of the key protecting that subject's fields in that tenant.</returns>
	/// <remarks>
	/// <para>
	/// <b>The composition is over the TOKEN rather than the raw identifier, and that is forced rather than
	/// chosen.</b> An erasure record persists only the token — correctly, since the raw identifier is
	/// personal data this framework has no reason to keep, and a request may legitimately be filed with the
	/// token itself and never carry the raw value at all. A derivation over the raw identifier would
	/// therefore be uncomputable on the erasure path, and the two paths MUST agree on one handle: two
	/// spellings of a key handle is how this defect returns. Injectivity is unaffected — the token is itself
	/// an HMAC of the identifier, so distinct subjects give distinct tokens, and distinct
	/// (tenant, token) pairs give distinct handles.
	/// </para>
	/// <para>
	/// <b>Unambiguous segment composition is mandatory, not stylistic.</b> Naive concatenation makes
	/// (tenant <c>"a"</c>, subject <c>"bc"</c>) collide with (tenant <c>"ab"</c>, subject <c>"c"</c>) — a
	/// cross-tenant collision introduced by the very change meant to remove one.
	/// <c>SegmentedKey.Compose</c> preserves segment boundaries by escaping separators inside each segment
	/// before the composed value is hashed.
	/// </para>
	/// <para>
	/// The handle is keyed by the deployment pepper for the same reason the token is: an unkeyed digest over
	/// a low-entropy identifier is reversible by dictionary attack, and the handle is a name a key store
	/// holds in the clear.
	/// </para>
	/// </remarks>
	internal static SubjectKeyHandle ForSubjectHash(
		TenantId tenant,
		string dataSubjectIdHash,
		IDataSubjectHasher hasher)
	{
		ArgumentNullException.ThrowIfNull(tenant);
		ArgumentNullException.ThrowIfNull(hasher);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		// THE CANONICAL COMPOSER, not a hand-rolled equivalent. This read
		// $"{tenant.Value.Length}:{tenant.Value}:{dataSubjectIdHash.Length}:{dataSubjectIdHash}" --
		// length-prefixed, and injective, and still wrong to write here. SegmentedKey.Compose exists for
		// exactly this, an architecture test enforces that every tenant key routes through it, and "mine is
		// also unambiguous" is the argument that gate exists to refuse. It was refusing it: the suite went
		// red at the commit that introduced the line above.
		//
		// It escapes rather than length-prefixes -- the separator is escaped inside each segment, so no pair
		// of segments can produce another pair's key -- which is a different route to the same property,
		// reviewed once in one place instead of re-argued at every call site.
		//
		// ESCAPING COULD LENGTHEN A SEGMENT AND THAT COSTS NOTHING HERE, which is why this is free rather
		// than a trade: the composed string is the HMAC's INPUT, never the handle. The handle is the digest,
		// 64 hex characters whatever the input length, so neither the key store's name-length limit nor the
		// retained-handle budget can be moved by an escape.
		return new SubjectKeyHandle(
			hasher.HashDataSubjectId(SegmentedKey.Compose(tenant.Value, dataSubjectIdHash)));
	}

	/// <summary>
	/// Composes the handle of the key protecting this subject's fields inside a RETAINED aggregate type.
	/// </summary>
	/// <param name="aggregateType">The retained aggregate type, as the event store records it.</param>
	/// <returns>The retained variant of this handle.</returns>
	/// <remarks>
	/// Composed ON TOP of the already-tenant-qualified handle, so the retained variant inherits the tenant
	/// term rather than restating it. Composition lives here, on the only type that can produce a handle, so
	/// a retained handle cannot be built from a bare pseudonymisation token — which is precisely the mistake
	/// the erasure path used to make.
	/// </remarks>
	internal SubjectKeyHandle Retained(string aggregateType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		return new SubjectKeyHandle($"{Value}-{RetainedKeyHandle.DiscriminatorFor(aggregateType)}");
	}

	/// <inheritdoc cref="Value"/>
	public override string ToString() => Value;

	/// <inheritdoc/>
	/// <remarks>
	/// Ordinal and case-sensitive, matching every handle comparison the key stores and the destruction
	/// records make.
	/// </remarks>
	public bool Equals(SubjectKeyHandle other) => string.Equals(_value, other._value, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is SubjectKeyHandle other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

	/// <summary>Reports whether two handles name the same key.</summary>
	/// <param name="left">The first handle.</param>
	/// <param name="right">The second handle.</param>
	/// <returns><see langword="true"/> when both name the same key.</returns>
	public static bool operator ==(SubjectKeyHandle left, SubjectKeyHandle right) => left.Equals(right);

	/// <summary>Reports whether two handles name different keys.</summary>
	/// <param name="left">The first handle.</param>
	/// <param name="right">The second handle.</param>
	/// <returns><see langword="true"/> when they name different keys.</returns>
	public static bool operator !=(SubjectKeyHandle left, SubjectKeyHandle right) => !left.Equals(right);
}
