// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Builds the per-contributor view of the declared erasure obligations.
/// </summary>
/// <remarks>
/// A contributor can only name what it erased if it is told which declared table-and-field pairs belong to
/// a store it covers: the pairs are consumer-chosen strings, so nothing in the pair itself says whose it is.
/// The routing therefore happens here, from the store kind recorded on the registration.
/// <para>
/// A pair registered with no store kind reaches no contributor and is discharged by nobody, so the coverage
/// gate keeps it outstanding and the erasure refuses to complete. That is the fail-closed direction and it
/// is deliberate: an unclassified obligation is one nobody has shown they erased.
/// </para>
/// </remarks>
internal static class DeclaredObligations
{
	/// <summary>
	/// Invokes one contributor with only the obligations that contributor covers.
	/// </summary>
	/// <param name="requestId">The erasure request.</param>
	/// <param name="status">The request's current status, source of the subject and tenant.</param>
	/// <param name="inventory">The discovered inventory, or <see langword="null"/> when discovery is off.</param>
	/// <param name="contributor">The contributor about to be invoked.</param>
	/// <param name="cancellationToken">Cancels the erasure.</param>
	/// <returns>What the contributor reported, including the obligations it named as discharged.</returns>
	public static async Task<ErasureContributorResult> EraseAsync(
		Guid requestId,
		ErasureStatus status,
		DataInventory? inventory,
		IErasureContributor contributor,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(status);
		ArgumentNullException.ThrowIfNull(contributor);

		var declared = inventory?.DeclaredLocationKinds is { Count: > 0 } kinds
			? kinds.Where(pair => contributor.CoveredStoreKinds.Contains(pair.Value))
				.Select(static pair => pair.Key)
				.ToList()
			: [];

		var context = new ErasureContributorContext
		{
			RequestId = requestId,
			DataSubjectIdHash = status.DataSubjectIdHash,
			IdType = status.IdType,
			TenantId = status.TenantId,
			Scope = status.Scope,
			DeclaredLocations = declared
		};

		return await contributor.EraseAsync(context, cancellationToken).ConfigureAwait(false);
	}
}
