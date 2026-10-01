// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Tests;

/// <summary>
/// Fakes for a key provider that CAN answer authoritatively whether a key is destroyed -- the only kind of
/// provider on which erasure may confirm key destruction. An unconfigured answer is <see langword="false"/>.
/// </summary>
internal static class KeyDestructionFakes
{
	public static IKeyManagementProvider ProviderThatReportsDestruction()
	{
		var provider = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyDestructionStatusProvider>());
		A.CallTo(() => provider.GetService(typeof(IKeyDestructionStatusProvider))).Returns(provider);
		return provider;
	}

	public static void ReportsDestroyed(this IKeyManagementProvider provider, string keyId, bool destroyed) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)provider).IsKeyDestroyedAsync(keyId, A<CancellationToken>._))
			.Returns(Task.FromResult(destroyed));

	/// <summary>
	/// A key admin that destroys keys AND can say, afterwards, that they are destroyed — which is what the
	/// framework requires of a provider whose keys an erasure shreds.
	/// </summary>
	/// <remarks>
	/// A bare <c>A.Fake&lt;IKeyManagementAdmin&gt;()</c> destroys keys and cannot answer whether they stayed
	/// destroyed, so an erasure using one cannot re-establish the key state before it attests and is never
	/// recorded as complete. That is the correct outcome for such a provider and the wrong fixture for an arm
	/// about anything else: every shipped key provider implements this capability, and startup validation
	/// warns a deployment whose provider does not.
	/// </remarks>
	public static IKeyManagementAdmin AdminThatReportsEveryKeyDestroyed()
	{
		var admin = AdminThatReportsAGenerationForEveryKey(alsoReportsDestruction: true);
		A.CallTo(() => ((IKeyDestructionStatusProvider)admin)
				.IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		return admin;
	}

	/// <summary>
	/// A key admin that can report the GENERATION behind a handle, which erasure reads before destroying it.
	/// </summary>
	/// <remarks>
	/// A bare <c>A.Fake&lt;IKeyManagementAdmin&gt;()</c> cannot answer this, and the consequence is not a
	/// detail: the destruction destroys the generation identifier, so erasure has to read it first, and
	/// without it there is nothing to write into the destruction ledger. A destruction with no ledger record
	/// is one no read of the subject's ciphertext can ever report, so erasure refuses to attest it. That is the
	/// correct outcome for such a provider and the wrong fixture for an arm about anything else: every shipped
	/// key provider implements <see cref="IKeyManagementProvider"/>.
	/// </remarks>
	/// <param name="alsoReportsDestruction">
	/// Whether the admin also answers the destruction-status capability. Declared at creation because
	/// FakeItEasy fixes a fake's interface set then.
	/// </param>
	public static IKeyManagementAdmin AdminThatReportsAGenerationForEveryKey(
		bool alsoReportsDestruction = false)
	{
		var admin = alsoReportsDestruction
			? A.Fake<IKeyManagementAdmin>(o => o
				.Implements<IKeyDestructionStatusProvider>()
				.Implements<IKeyManagementProvider>())
			: A.Fake<IKeyManagementAdmin>(o => o.Implements<IKeyManagementProvider>());

		// MINTED PER HANDLE, not derived FROM it. Distinct handles must stay distinct in the record -- a fixed
		// literal would make two destroyed keys collide on the record's key and silently record only the first --
		// and the same handle must answer the same generation, because a retried erasure attests what the first
		// pass destroyed. A map from handle to a minted generation gives both. Deriving the value from the handle
		// gave both too, and is exactly what KeyGeneration's consumer obligation forbids: a derived generation
		// collides across subjects, and a collision under a generation-keyed ledger reports one subject's live
		// data as erased by another's erasure. The fake no longer models a provider that breaks that rule.
		var generations = new System.Collections.Concurrent.ConcurrentDictionary<string, KeyGeneration>(
			StringComparer.Ordinal);

		A.CallTo(() => ((IKeyManagementProvider)admin).GetKeyAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, CancellationToken _) =>
				Task.FromResult<KeyMetadata?>(new KeyMetadata
				{
					KeyId = keyId,
					Version = 1,
					Status = KeyStatus.Active,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = DateTimeOffset.UtcNow,
					Generation = generations.GetOrAdd(keyId, static _ => KeyGeneration.Mint()),
				}));

		return admin;
	}

	public static void ReportsEveryKeyDestroyed(this IKeyManagementProvider provider) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)provider).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
}
