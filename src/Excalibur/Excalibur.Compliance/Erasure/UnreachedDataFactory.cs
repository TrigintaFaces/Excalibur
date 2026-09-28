// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Turns what the coverage gate and the erasure contributors reported into the certificate's record of
/// what was NOT reached.
/// </summary>
/// <remarks>
/// <para>
/// Separate from the service on purpose, and not only for class-coupling budget: this is the one place
/// that decides how an unmet obligation is worded on a signed document, and it should be readable and
/// reviewable without reading the execution path around it.
/// </para>
/// <para>
/// It runs on the execution path because that is the only place the residue exists as DATA. The
/// persisted status keeps counts and one joined error string, so anything reconstructed afterwards
/// would have to parse prose back into structure — not a contract, and it would change meaning the
/// first time someone reworded a message.
/// </para>
/// <para>
/// Every entry it produces asserts that no lawful basis is claimed. An item that DOES have a basis is
/// an exemption and belongs on the certificate's other list. The two never mix: presenting an unmet
/// obligation as a lawful exemption is the failure this record exists to prevent.
/// </para>
/// </remarks>
internal static class UnreachedDataFactory
{
	/// <summary>Builds the certificate's unreached-data entries.</summary>
	/// <param name="uncoveredStoreKinds">Store kinds the coverage gate found no eraser for.</param>
	/// <param name="failedContributors">Contributors that ran and did not complete, with their reason.</param>
	/// <returns>One entry per place the erasure did not reach; empty when it reached everything.</returns>
	internal static IReadOnlyList<UnreachedDataLocation> Build(
		IReadOnlyCollection<string> uncoveredStoreKinds,
		IReadOnlyList<(string Name, string? Error)> failedContributors)
	{
		ArgumentNullException.ThrowIfNull(uncoveredStoreKinds);
		ArgumentNullException.ThrowIfNull(failedContributors);

		var entries = new List<UnreachedDataLocation>(uncoveredStoreKinds.Count + failedContributors.Count);

		foreach (var kind in uncoveredStoreKinds)
		{
			entries.Add(new UnreachedDataLocation
			{
				StoreKind = kind,
				Mechanism =
					"No component was registered that erases data of this kind, so the erasure did not reach "
					+ "it and the data remains where it was.",
				ControllerObligation =
					"Erasing this data remains the controller's responsibility and must be carried out through "
					+ "whatever mechanism owns that store.",
				RemediationCost = CostFor(kind),
			});
		}

		foreach (var (name, error) in failedContributors)
		{
			entries.Add(new UnreachedDataLocation
			{
				StoreKind = name,

				// The component's own words, because it is the thing that knows why. Copied rather than
				// summarised, so the certificate does not paraphrase a mechanism it does not model.
				Mechanism = string.IsNullOrWhiteSpace(error)
					? "The component responsible for erasing this data did not complete, and reported no reason."
					: error,
				ControllerObligation =
					"The data this component is responsible for has not been erased. Discharging that "
					+ "obligation remains the controller's.",
				RemediationCost = RemediationCost.Unknown,
			});
		}

		return entries;
	}

	/// <summary>What discharging an outstanding obligation costs, where that is known here.</summary>
	/// <param name="storeKind">The store kind not reached.</param>
	/// <returns>The cost class.</returns>
	/// <remarks>
	/// <see cref="RemediationCost.Unknown"/> is the honest default and is returned wherever the cost is
	/// genuinely not known. A certificate that guessed would be worse than one that says it does not know,
	/// because a controller plans around the number it is given.
	/// </remarks>
	private static RemediationCost CostFor(string storeKind) =>
		string.Equals(storeKind, DataStoreKind.Projection.Value, StringComparison.OrdinalIgnoreCase)
			? RemediationCost.RequiresReadModelOffline
			: RemediationCost.Unknown;
}
