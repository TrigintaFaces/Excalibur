// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

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

	public static void ReportsEveryKeyDestroyed(this IKeyManagementProvider provider) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)provider).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

	/// <summary>
	/// A key admin that also answers as a key provider and reports a generation for every handle.
	/// </summary>
	/// <returns>The admin.</returns>
	/// <remarks>
	/// <para>
	/// An erasure CANNOT complete against an admin that reports no generation, and that is by design: the
	/// destruction stages the generation before destroying, because the identifier a destruction record needs is
	/// destroyed along with the material. A key whose generation cannot be read is therefore not destroyed at
	/// all, rather than destroyed and unrecordable -- which would leave the subject permanently undecryptable
	/// with no repair available. A bare <c>A.Fake&lt;IKeyManagementAdmin&gt;()</c> reports none, so every arm
	/// built on one tests the refusal instead of the behaviour it names.
	/// </para>
	/// <para>
	/// MINTED PER HANDLE, not derived FROM it. Stable for one handle, because a retried erasure must attest what
	/// the first pass destroyed; distinct across handles, so two destroyed keys do not collide on the record's
	/// key; and not a function of the handle, which is what the generation type's consumer obligation forbids --
	/// a derived generation collides across subjects, and a collision under a generation-keyed ledger reports one
	/// subject's live data as erased by another subject's erasure.
	/// </para>
	/// </remarks>
	public static IKeyManagementAdmin AdminThatReportsAGenerationForEveryHandle()
	{
		var admin = A.Fake<IKeyManagementAdmin>(o => o.Implements<IKeyManagementProvider>());

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
}
