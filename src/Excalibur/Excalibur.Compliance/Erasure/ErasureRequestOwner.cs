// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Resolves the tenant that OWNS the key material an erasure request targets.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is one function and not three expressions.</b> The in-memory, PostgreSQL and SQL Server
/// erasure stores each persist an owner term for the request, and each used to compute it inline with the
/// same expression character for character. That is a contract implemented three times, so an error in it
/// replicates three times — and it did: every one of them resolved the owner from a value the caller had
/// put on the request, with nothing comparing it to the term the write path had actually encrypted under.
/// </para>
/// <para>
/// <b>What goes wrong when the two disagree.</b> The per-subject key handle is derived from the owner. The
/// write derives it from the ambient tenant context; if the erasure derives it from anything else, it
/// destroys a handle that was never minted. The provider answers "not found", the erasure treats that as an
/// idempotent no-op, no error is recorded, and the request reaches a completed state with a signed
/// certificate over personal data that still decrypts. Nothing throws and nothing is logged, so the failure
/// is invisible from outside — which is what makes it severe rather than merely wrong.
/// </para>
/// <para>
/// <b>THE AMBIENT CONTEXT'S TERM IS AUTHORITATIVE, NOT THE AMBIENT SCOPE'S.</b> This distinction is
/// load-bearing and easy to get backwards. In single-tenant mode the stores' ambient <i>scope</i> is the
/// reserved untenanted partition, while the <i>context</i> resolves the deployment's own default identity.
/// The write path reads the context. So the owner is read from the context here too — resolving it from the
/// scope instead would make every single-tenant host write under one identity and erase under another,
/// which is the same silent no-op erasure wearing different clothes.
/// </para>
/// <para>
/// <b>A mismatch is refused, not corrected.</b> There is exactly one legitimate owner for a request, so a
/// caller-supplied value either agrees with it and adds nothing, or disagrees with it and selects the wrong
/// key. Substituting the right term silently would close the safety hole while discarding what the caller
/// asked for without telling them, which is the same dishonesty one layer down. The request is rejected by
/// name so the caller learns the deployment does not have the tenant they named.
/// </para>
/// </remarks>
internal static class ErasureRequestOwner
{
	/// <summary>
	/// Resolves the owning tenant for an erasure request, refusing a request that names a different one.
	/// </summary>
	/// <param name="requireTenant">
	/// Whether multi-tenancy is active, as selected by the tenant-context options rather than by whether a
	/// context happens to be supplied.
	/// </param>
	/// <param name="ambientScope">The ambient tenant scope, resolved by the caller before this call.</param>
	/// <param name="request">The erasure request whose owner is being resolved.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Its resolved term is what the write path derived its key handle from, so
	/// it is the authority here.
	/// </param>
	/// <returns>The owning tenant's identifier, validated and never empty.</returns>
	/// <exception cref="ErasureOperationException">
	/// The request names a tenant other than the one this deployment resolved.
	/// </exception>
	internal static string Resolve(
		bool requireTenant,
		TenantScope ambientScope,
		ErasureRequest request,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(tenantContext);

		// Under multi-tenancy the resolved scope is the authority, and the caller has already failed closed
		// on an unresolved tenant by the time it gets here. Otherwise the context's own resolved identity is
		// the authority -- see the remarks: the scope is NOT interchangeable with the context here.
		var authoritative = requireTenant ? ambientScope.TenantId : tenantContext.TenantId;

		// A TenantId constructed from the authoritative term, so an absent or malformed one fails here
		// rather than downstream at a derivation that cannot report what went wrong.
		var owner = new TenantId(authoritative!).Value;

		// An UNSET request tenant is the ordinary case and carries no disagreement: the single-tenant shape
		// is documented as leaving it unset, because the property exists for multi-tenant callers.
		//
		// NULL ONLY, AND NOT IsNullOrWhiteSpace, which is the distinction this line used to lose. A caller
		// who writes TenantId = "" or "   " has SET the property, to something that is not a tenant. Reading
		// that as "unset" and substituting the resolved owner silently accepts a malformed claim and hides a
		// caller error -- and the claim is about which key protects a data subject's personal data, so it is
		// the last place to be lenient. Present means it is a claim, and a claim must be well formed.
		if (request.TenantId is null)
		{
			return owner;
		}

		// TenantId's own constructor is the validator, reused rather than re-expressed, so there is exactly
		// one definition of a well-formed tenant in the framework and this path cannot drift from it. It
		// throws ArgumentException for an empty or whitespace term, which is what a malformed claim is.
		var claimed = new TenantId(request.TenantId).Value;

		// Ordinal, with no trimming and no case folding, for the same reason the read path compares its
		// envelope owner that way: two terms differing only in case are two tenants, and a comparison that
		// folded them would hand one tenant's key to the other.
		if (!string.Equals(claimed, owner, StringComparison.Ordinal))
		{
			throw ErasureOperationException.ValidationFailed(
				request.RequestId,
				$"The erasure request names tenant '{request.TenantId}', but this deployment resolved "
				+ $"'{owner}'. The owning tenant decides which key protects the subject's data, and a key "
				+ "derived from a tenant the write path never used would destroy nothing while reporting a "
				+ "completed erasure. File the request for the resolved tenant, or leave its tenant unset "
				+ "to accept the resolved one.");
		}

		return owner;
	}
}
