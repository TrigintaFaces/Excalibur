// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Partitions discovered personal-data locations by GDPR erasure coverage (ADR-336 Amendment 1/1a,
/// key-aware 3-state model). Extracted from <see cref="ErasureService"/> so the coverage gate is a single,
/// named, testable unit and the orchestration method stays within its class-coupling budget.
/// </summary>
internal static class ErasureCoverageEvaluator
{
	// Store-kinds exempt from erasure BY DEFAULT — a declared, documented exemption (with legal basis), NOT
	// an uncovered gap. The audit/security store is exempt under GDPR Art.17(3)(b) (legal-obligation
	// retention) + Art.17(3)(e) (legal claims / incident investigation). A consumer overrides by registering
	// an IErasureContributor for the audit store-kind (contributor coverage wins over the default exemption).
	private static readonly IReadOnlyDictionary<DataStoreKind, ExemptionPolicy> DefaultExemptions =
		new Dictionary<DataStoreKind, ExemptionPolicy>
		{
			[DataStoreKind.Audit] = new ExemptionPolicy(
				LegalHoldBasis.LegalObligation,
				"Audit/security store retained under GDPR Art.17(3)(b) (compliance with a legal obligation: "
				+ "security audit-trail retention) and Art.17(3)(e) (establishment/exercise/defence of legal "
				+ "claims: security-incident investigation)."),
		};

	/// <summary>
	/// Classifies each location: covered by (a) crypto-shred (its key was deleted), (b) a registered
	/// contributor for its store-kind, or (c) a declared exemption. Uncovered locations (none of a/b/c)
	/// must force a non-<c>Completed</c> outcome; exempt locations are non-blocking but are enumerated
	/// (with legal basis) so the certificate is explicit about what it deliberately did not erase (FR-4a).
	/// </summary>
	/// <param name="locations">The discovered personal-data locations.</param>
	/// <param name="deletedKeyIds">The per-subject key IDs that were actually deleted (crypto-shred).</param>
	/// <param name="contributors">The registered erasure contributors.</param>
	/// <returns>The uncovered store-kind names and the exemptions actually in play for this erasure.</returns>
	public static CoverageOutcome Evaluate(
		IReadOnlyList<DataLocation> locations,
		IReadOnlyCollection<string> deletedKeyIds,
		IEnumerable<IErasureContributor> contributors)
	{
		var deletedKeySet = new HashSet<string>(deletedKeyIds, StringComparer.Ordinal);

		var contributorCoveredKinds = new HashSet<DataStoreKind>();
		foreach (var contributor in contributors)
		{
			foreach (var kind in contributor.CoveredStoreKinds)
			{
				// Unknown is never coverable (safety default) — ignore a contributor that wrongly declares it.
				if (!kind.IsUnknown)
				{
					_ = contributorCoveredKinds.Add(kind);
				}
			}
		}

		var uncovered = new HashSet<string>(StringComparer.Ordinal);
		var exemptions = new List<ErasureException>();
		foreach (var location in locations)
		{
			// (a) crypto-shred: the per-subject key protecting this field was deleted.
			if (deletedKeySet.Contains(location.KeyId))
			{
				continue;
			}

			// (b) a registered contributor covers this store-kind (row-delete/tombstone).
			if (!location.StoreKind.IsUnknown && contributorCoveredKinds.Contains(location.StoreKind))
			{
				continue;
			}

			// (c) a declared exemption — non-blocking, but enumerated on the certificate with its legal basis
			// (FR-4a: explicit, never silent). ErasureException.Basis is required, so a basis-less exemption
			// is structurally inexpressible.
			if (!location.StoreKind.IsUnknown
				&& DefaultExemptions.TryGetValue(location.StoreKind, out var policy))
			{
				exemptions.Add(new ErasureException
				{
					Basis = policy.Basis,
					DataCategory = location.DataCategory,
					Reason = $"Store '{location.StoreKind.Value}': {policy.Reason}"
				});
				continue;
			}

			_ = uncovered.Add(location.StoreKind.Value);
		}

		return new CoverageOutcome(uncovered, exemptions);
	}

	/// <summary>The legal basis and reason for a default store-kind erasure exemption (FR-4a).</summary>
	private sealed record ExemptionPolicy(LegalHoldBasis Basis, string Reason);
}

/// <summary>The result of partitioning discovered locations by erasure coverage.</summary>
internal sealed record CoverageOutcome(
	IReadOnlyCollection<string> UncoveredStoreKinds,
	IReadOnlyList<ErasureException> Exemptions);
