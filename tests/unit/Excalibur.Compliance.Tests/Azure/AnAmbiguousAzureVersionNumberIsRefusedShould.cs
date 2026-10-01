// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Azure;
using Azure.Security.KeyVault.Keys;

using Excalibur.Compliance.Azure;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Azure;

/// <summary>
/// Azure Key Vault versions are opaque identifiers with no ordinal, so this provider derives an integer from
/// each one — and that derivation is not injective. When two of a key's versions derive the same number, the
/// lookup must refuse rather than resolve one of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>What resolving one of them costs.</b> The metadata returned carries the REQUESTED number while naming a
/// different version's material. A decrypt then fetches the wrong key, and because the number is bound into
/// the AES-GCM associated data the tag fails to verify — so the consumer sees a corrupt-ciphertext error for a
/// value whose key is present and perfectly healthy, with nothing anywhere naming the collision.
/// </para>
/// <para>
/// <b>Why the derivation is still there rather than deleted.</b> The number it produces is written onto every
/// envelope encrypted under an Azure-backed key, is what a decrypt uses to find the key again, and is bound
/// into the authentication tag. Changing it — to an ordering-based index, a wider hash, or a constant — makes
/// every value already encrypted under an Azure key undecryptable twice over. So the collision is guarded
/// rather than removed, and these arms are that guard.
/// </para>
/// <para>
/// <b>The colliding pair is FOUND, not hardcoded.</b> A pinned pair would stop being a collision the moment
/// the derivation changed, and the arm would then pass while proving nothing. Searching for a real pair
/// through the provider's own derivation keeps the arm bound to whatever the derivation currently is.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AnAmbiguousAzureVersionNumberIsRefusedShould
{
	private const string KeyId = "dispatch-orders";
	private const string VaultUri = "https://unit-tests.vault.azure.net/";

	/// <summary>
	/// The premise, measured rather than asserted: the derivation maps two distinct Azure versions to one
	/// number.
	/// </summary>
	/// <remarks>
	/// If this ever fails because no collision can be found, the derivation has become injective and the
	/// refusal below is no longer reachable — at which point the guard should be revisited, not deleted
	/// silently. That is why the premise has its own arm.
	/// </remarks>
	[Fact]
	public void ShowThatTwoDistinctVersionsCanDeriveTheSameNumber()
	{
		var (first, second, number) = FindCollidingVersions();

		first.ShouldNotBe(second, "the two Azure version identifiers must be genuinely different.");
		DeriveVersionNumber(first).ShouldBe(number);
		DeriveVersionNumber(second).ShouldBe(number);
	}

	/// <summary>
	/// SAFETY, and the RED arm. A version number two of the key's versions map to is refused, not resolved.
	/// </summary>
	/// <remarks>
	/// RED input: a key whose versions include a colliding pair, looked up by the number they share. Before the
	/// guard, the first match in enumeration order was returned and stamped with the requested number.
	/// </remarks>
	[Fact]
	public async Task RefuseToResolveANumberThatTwoVersionsShare()
	{
		var (first, second, number) = FindCollidingVersions();

		using var sut = CreateProvider(VersionsOf(first, second));

		var ex = await Should.ThrowAsync<EncryptionException>(
			() => sut.GetKeyVersionAsync(KeyId, number, TestContext.Current.CancellationToken))
			.ConfigureAwait(true);

		ex.Message.ShouldContain(
			first,
			customMessage: "the refusal must name the ambiguous versions, or an operator cannot act on it.");
		ex.Message.ShouldContain(second);
	}

	/// <summary>
	/// LIVENESS. A number exactly one version maps to still resolves.
	/// </summary>
	/// <remarks>
	/// Without this, a lookup that refused EVERY version number would satisfy the arm above — which is the
	/// cheapest way never to answer about the wrong version and the most expensive way to be wrong.
	/// </remarks>
	[Fact]
	public async Task StillResolveANumberOnlyOneVersionMapsTo()
	{
		var (first, second, _) = FindCollidingVersions();

		// Only the first of the colliding pair is present, so its number is unambiguous.
		using var sut = CreateProvider(VersionsOf(first));

		var metadata = await sut.GetKeyVersionAsync(KeyId, DeriveVersionNumber(first), TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		metadata.ShouldNotBeNull(
			"an unambiguous version must still resolve; refusing everything is not a fix.");
		metadata.Version.ShouldBe(DeriveVersionNumber(first));
		_ = second;
	}

	/// <summary>
	/// LIVENESS. A number no version maps to is still reported absent rather than refused.
	/// </summary>
	/// <remarks>
	/// "Not found" and "ambiguous" are different answers and must stay different: a caller that cannot tell
	/// them apart cannot tell a version that never existed from one this provider declines to identify.
	/// </remarks>
	[Fact]
	public async Task StillReportANumberNoVersionMapsToAsAbsent()
	{
		var (first, _, _) = FindCollidingVersions();

		using var sut = CreateProvider(VersionsOf(first));

		var absent = DeriveVersionNumber(first) == 1 ? 2 : 1;

		var metadata = await sut.GetKeyVersionAsync(KeyId, absent, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		metadata.ShouldBeNull();
	}

	// Searches the provider's OWN derivation for two distinct version identifiers that share a number. The
	// codomain is 100000 buckets, so a pair appears well inside this bound; the loop is deterministic because
	// the candidates and the derivation both are.
	private static (string First, string Second, int Number) FindCollidingVersions()
	{
		var seen = new Dictionary<int, string>();

		for (var i = 0; i < 20000; i++)
		{
			var candidate = $"v{i:x8}";
			var number = DeriveVersionNumber(candidate);

			if (seen.TryGetValue(number, out var earlier))
			{
				return (earlier, candidate, number);
			}

			seen[number] = candidate;
		}

		throw new InvalidOperationException(
			"No colliding pair was found in 20000 candidates. The derivation may have become injective, in "
			+ "which case the ambiguity guard is unreachable and should be revisited deliberately rather than "
			+ "left as an untested branch.");
	}

	private static int DeriveVersionNumber(string version) =>
		InvokeExtractVersionNumber(PropertiesFor(version));

	private static int InvokeExtractVersionNumber(KeyProperties properties)
	{
		var method = typeof(AzureKeyVaultProvider).GetMethod(
			"ExtractVersionNumber",
			BindingFlags.NonPublic | BindingFlags.Static);

		method.ShouldNotBeNull();

		return (int)method!.Invoke(null, [properties])!;
	}

	private static KeyProperties PropertiesFor(string version) =>
		KeyModelFactory.KeyProperties(
			id: new Uri($"{VaultUri}keys/{KeyId}/{version}"),
			vaultUri: new Uri(VaultUri),
			name: KeyId,
			version: version,
			managed: false,
			createdOn: DateTimeOffset.UtcNow.AddDays(-2),
			updatedOn: DateTimeOffset.UtcNow.AddDays(-1),
			recoveryLevel: "Recoverable");

	private static KeyProperties[] VersionsOf(params string[] versions) =>
		[.. versions.Select(PropertiesFor)];

	private static AzureKeyVaultProvider CreateProvider(KeyProperties[] versions)
	{
		var sut = new AzureKeyVaultProvider(
			Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri(VaultUri),
				KeyNamePrefix = string.Empty,
			}),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<AzureKeyVaultProvider>.Instance);

		var keyClient = A.Fake<KeyClient>(o => o.WithArgumentsForConstructor(() =>
			new KeyClient(new Uri(VaultUri), new global::Azure.Identity.DefaultAzureCredential())));

		A.CallTo(() => keyClient.GetPropertiesOfKeyVersionsAsync(KeyId, A<CancellationToken>._))
			.Returns(AsyncPageable<KeyProperties>.FromPages(
				[Page<KeyProperties>.FromValues(versions, continuationToken: null, A.Fake<Response>())]));

		foreach (var properties in versions)
		{
			var key = KeyModelFactory.KeyVaultKey(properties, new JsonWebKey(System.Security.Cryptography.RSA.Create(2048), includePrivateParameters: false));

			A.CallTo(() => keyClient.GetKeyAsync(KeyId, properties.Version, A<CancellationToken>._))
				.Returns(Task.FromResult(Response.FromValue(key, A.Fake<Response>())));
		}

		var field = typeof(AzureKeyVaultProvider).GetField("_keyClient", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(sut, keyClient);

		return sut;
	}
}
