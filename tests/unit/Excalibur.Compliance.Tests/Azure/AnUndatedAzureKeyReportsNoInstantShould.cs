// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Azure;
using Azure.Security.KeyVault.Keys;

using Excalibur.Compliance;
using Excalibur.Compliance.Azure;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Azure;

/// <summary>
/// A Key Vault key whose properties carry no creation instant reports <see langword="null"/>, not the local
/// clock — and the read still succeeds.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THE FABRICATION IS WORSE THAN AN ABSENCE.</b> The provider's active-key resolution orders keys by
/// <see cref="KeyMetadata.CreatedAt"/> and takes the most recent. Substituting <c>UtcNow</c> for an unknown
/// instant therefore does not degrade that choice — it INVERTS it: the one key whose age is unknown sorts
/// ahead of every key whose age is known, and is selected as the newest. A caller cannot tell a fabricated
/// instant from a measured one, so nothing downstream ever learns the ordering was decided by a value the
/// backend never supplied.
/// </para>
/// <para>
/// <b>Reported, not refused.</b> A refusal was tried here and withdrawn: the sibling provider's equivalent
/// throw broke 48 arms against real infrastructure, because a backend that omits an instant is a normal
/// backend and this is the ordinary metadata path. Failing a read the provider can perfectly well answer, to
/// protect an ordering almost no caller performs, costs more than it buys. The absence travels to the caller,
/// which owes it the treatment on <see cref="KeyMetadata.CreatedAt"/>: unknown counts as stale.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AnUndatedAzureKeyReportsNoInstantShould
{
	private const string VaultUri = "https://unit-test-vault.vault.azure.net/";
	private const string KeyId = "orders";

	/// <summary>
	/// THE LOAD-BEARING ARM. No creation instant in the response still reads, and reports
	/// <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// RED on the fabrication: coalesce the absent instant to <c>UtcNow</c> and this reports a creation time
	/// no backend ever gave, one that sorts ahead of every real key. RED on the over-correction too: throw
	/// instead, and an ordinary read of a key Key Vault happens not to date fails outright. Both assertions
	/// are here because either one alone permits the other failure.
	/// </remarks>
	[Fact]
	public async Task ReportNoInstant_AndStillRead_WhenKeyVaultSuppliesNone()
	{
		using var sut = CreateProvider(createdOn: null);

		var metadata = await sut.GetKeyAsync(KeyId, CancellationToken.None);

		metadata.ShouldNotBeNull(
			"a key with no creation instant is still a key, and describing it must not fail");

		metadata!.CreatedAt.ShouldBeNull(
			"unknown must be expressible. Any value here is a fabrication indistinguishable from a "
			+ "measurement, and a substituted clock sorts this key as the most recently created");
	}

	/// <summary>
	/// LIVENESS. Without it the arm above is satisfied by a provider that refuses every key, which would make
	/// the whole Key Vault metadata path unusable.
	/// </summary>
	[Fact]
	public async Task StillDescribeAKeyThatCarriesACreationInstant()
	{
		var created = DateTimeOffset.UtcNow.AddDays(-30);
		using var sut = CreateProvider(createdOn: created);

		var metadata = await sut.GetKeyAsync(KeyId, CancellationToken.None);

		metadata.ShouldNotBeNull();
		metadata!.CreatedAt.ShouldBe(
			created,
			"a key that reports its creation instant must carry that instant through unchanged — the absent "
			+ "case must not have been turned into a blanket null");
	}

	private static AzureKeyVaultProvider CreateProvider(DateTimeOffset? createdOn)
	{
		var sut = new AzureKeyVaultProvider(
			Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri(VaultUri),
				KeyNamePrefix = string.Empty,
			}),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var properties = KeyModelFactory.KeyProperties(
			id: new Uri($"{VaultUri}keys/{KeyId}/abcdef0123456789abcdef0123456789"),
			vaultUri: new Uri(VaultUri),
			name: KeyId,
			version: "abcdef0123456789abcdef0123456789",
			managed: false,
			createdOn: createdOn,
			updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
			recoveryLevel: "Recoverable");

		var key = KeyModelFactory.KeyVaultKey(
			properties,
			new JsonWebKey(System.Security.Cryptography.RSA.Create(2048), includePrivateParameters: false));

		var keyClient = A.Fake<KeyClient>(o => o.WithArgumentsForConstructor(() =>
			new KeyClient(new Uri(VaultUri), new global::Azure.Identity.DefaultAzureCredential())));

		A.CallTo(() => keyClient.GetKeyAsync(KeyId, A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));

		var field = typeof(AzureKeyVaultProvider).GetField(
			"_keyClient", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(sut, keyClient);

		return sut;
	}
}
