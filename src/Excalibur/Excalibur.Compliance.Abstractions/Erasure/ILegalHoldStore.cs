// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Storage abstraction for legal holds.
/// </summary>
/// <remarks>
/// <para>
/// Legal holds support GDPR Article 17(3) exceptions
/// that block erasure when data must be retained for legal reasons.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every member here is confined to the ambient tenant established for this
/// store instance: <see cref="GetHoldAsync"/> reports a hold stored under another tenant as not found
/// rather than resolving it — a real authorization boundary, since a hold identifier is exactly the kind
/// of caller-supplied value a foreign tenant could otherwise probe with — and <see cref="UpdateHoldAsync"/>
/// can neither read nor overwrite another tenant's hold for the same identifier. Which mechanism a given
/// provider uses to hold that boundary is declared by its capability marker —
/// <see cref="ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant — and the
/// package's own <c>ARCHITECTURE.md</c> states the falsifiable guarantee and how it is verified. A store
/// presenting no marker is not confined by the framework.
/// </para>
/// </remarks>
[TenantOwned]
public interface ILegalHoldStore
{
	/// <summary>
	/// Saves a new legal hold. This is a save, never an upsert: a hold identifier already in use is
	/// rejected rather than overwritten.
	/// </summary>
	/// <param name="hold">The legal hold to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// <b>The hold identifier namespace is estate-wide, unlike the reads on this interface.</b> Every
	/// shipped relational provider declares the hold identifier as the sole primary key of its table, so an
	/// identifier is unique across the whole estate rather than within one tenant. That is deliberate, and it
	/// is stated here because it is the one place this interface is wider than its type-level confinement
	/// paragraph, and a caller who assumes otherwise misreads the result.
	/// </para>
	/// <para>
	/// The consequence a caller must plan for: under multi-tenancy this operation can report a duplicate for
	/// a hold that <see cref="GetHoldAsync"/> reports as absent, because the colliding hold belongs to
	/// another tenant and is — correctly — not visible. Both answers are true. A caller that must not
	/// disclose whether an identifier is in use allocates hold identifiers randomly rather than deriving
	/// them from a case reference or any other value another tenant could guess.
	/// </para>
	/// </remarks>
	/// <exception cref="DuplicateLegalHoldException">
	/// A hold with the same <see cref="LegalHold.HoldId"/> is already stored — raised if and only if that is
	/// the reason, so a caller can tell it apart from the other conditions that surface as an
	/// <see cref="InvalidOperationException"/> here.
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established. The hold is <b>not</b> stored. This
	/// also derives from <see cref="InvalidOperationException"/>, which is why the duplicate condition has a
	/// type of its own: catching the base type here treats a hold that was never written as one already on
	/// file, and a legal hold silently dropped does not fail safe — the next erasure runs and succeeds.
	/// </exception>
	Task SaveHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a legal hold by ID.
	/// </summary>
	/// <param name="holdId">The hold ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The legal hold, or null if not found.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a hold stored under another
	/// tenant is reported as not found, the same as a <paramref name="holdId"/> that never existed.
	/// </remarks>
	Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates a legal hold.
	/// </summary>
	/// <param name="hold">The hold with updated values.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the hold was updated.</returns>
	Task<bool> UpdateHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="ILegalHoldQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
