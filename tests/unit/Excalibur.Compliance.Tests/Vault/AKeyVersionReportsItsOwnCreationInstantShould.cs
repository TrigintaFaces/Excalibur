// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Net;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Compliance.Vault;

using FakeItEasy;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Excalibur.Compliance.Tests.Vault;

/// <summary>
/// <see cref="KeyMetadata.CreatedAt"/> describes the VERSION the metadata is about, not whichever version
/// happened to be first — and when the backend reports no instant for it, the metadata carries
/// <see langword="null"/> rather than the local clock.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT THIS LOCKS.</b> The mapping read its creation time from a hardcoded version key of
/// <c>"1"</c> while the version actually being described sat in a local two lines above it. So every version
/// of a handle reported one identical instant: the field could not order versions, and any caller comparing
/// two versions of a handle by it got equality. The status on the same metadata had already been corrected
/// for exactly this — computed from the key and ignoring the version argument — and this field was left
/// behind by that fix, which is why a sibling sweep matters more than a single reported symptom.
/// </para>
/// <para>
/// <b>AND WHY THE FABRICATION IS WORSE THAN THE WRONG VERSION.</b> The absent case substituted
/// <c>UtcNow</c>, which is not a degraded answer but an inverted one: the oldest material reports as the
/// newest. A point-in-time version lookup compares against this field and stops at the first version that
/// postdates the requested instant, so one fabricated value truncates the scan and resolves to an older
/// version than the one that was live — silently, and in the unsafe direction.
/// </para>
/// <para>
/// <b>The remedy is a reported absence, NOT a refusal, and that distinction was learned the hard way.</b>
/// Refusing the read looked equivalent and was not: real Transit does not date every version it holds, so a
/// throw on this path fails an ORDINARY metadata read for any such key. The absence travels to the caller
/// instead, which owes it the treatment documented on <see cref="KeyMetadata.CreatedAt"/> — unknown counts
/// as stale, never as recent. A provider that cannot answer an ordering question may still refuse it; one
/// that is merely describing a key may not.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AKeyVersionReportsItsOwnCreationInstantShould
{
	private const string KeyId = "orders";

	private static readonly DateTimeOffset VersionOneCreated = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset VersionTwoCreated = new(2026, 6, 15, 12, 30, 0, TimeSpan.Zero);

	/// <summary>
	/// THE LOAD-BEARING ARM. Version 2 reports version 2's instant.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: read the creation time from a hardcoded <c>"1"</c> and version 2 reports
	/// version 1's instant, so the two versions of this handle become indistinguishable by date.
	/// </remarks>
	[Fact]
	public async Task ReportTheRequestedVersionsOwnCreationInstant_NotTheFirstVersions()
	{
		var provider = CreateProvider(TransitHolding(VersionOneCreated, VersionTwoCreated));

		var second = await provider.GetKeyVersionAsync(KeyId, 2, CancellationToken.None);

		second.ShouldNotBeNull();
		second!.CreatedAt.ShouldBe(
			VersionTwoCreated,
			"the metadata describes version 2, so it must carry version 2's creation instant. Version 1's "
			+ "instant here means every version of this handle reports one date and the field cannot order "
			+ "them");
	}

	/// <summary>
	/// The other half of the same property, and it is not redundant: an arm that only checked version 2 would
	/// pass against a mapping that read the LAST version for everything.
	/// </summary>
	[Fact]
	public async Task ReportVersionOnesInstantForVersionOne()
	{
		var provider = CreateProvider(TransitHolding(VersionOneCreated, VersionTwoCreated));

		var first = await provider.GetKeyVersionAsync(KeyId, 1, CancellationToken.None);

		first.ShouldNotBeNull();
		first!.CreatedAt.ShouldBe(VersionOneCreated);
	}

	/// <summary>
	/// The two versions of one handle must be DISTINGUISHABLE by this field, which is the property a caller
	/// ordering or comparing versions actually depends on.
	/// </summary>
	/// <remarks>
	/// Stated separately from the two arms above because it is what the defect destroyed: both of those could
	/// in principle be satisfied while some third version collapsed onto another. This asserts the
	/// relationship, in creation order, rather than two independent values.
	/// </remarks>
	[Fact]
	public async Task OrderTwoVersionsOfOneHandleByTheirOwnInstants()
	{
		var provider = CreateProvider(TransitHolding(VersionOneCreated, VersionTwoCreated));

		var first = await provider.GetKeyVersionAsync(KeyId, 1, CancellationToken.None);
		var second = await provider.GetKeyVersionAsync(KeyId, 2, CancellationToken.None);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();

		// Both instants must be PRESENT for an ordering to mean anything, and this fixture dates both
		// versions — so their absence would be a broken fixture rather than a tolerated state.
		var firstCreated = first!.CreatedAt.ShouldNotBeNull();
		var secondCreated = second!.CreatedAt.ShouldNotBeNull();

		firstCreated.ShouldBeLessThan(
			secondCreated,
			"a later version was created later. Equality here is the original defect, and it makes every "
			+ "ordering built on this field return an arbitrary element");
	}

	/// <summary>
	/// THE LOAD-BEARING ARM FOR THE ABSENT CASE. A response carrying no instant for the version still reads,
	/// and reports <see langword="null"/> rather than a substituted value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// RED on the defect this locks: initialize the instant to <c>UtcNow</c> and this read reports a
	/// fabricated date that no caller can tell from a measured one, and which sorts as the newest.
	/// </para>
	/// <para>
	/// RED ALSO on the over-correction: throw instead of reporting the absence and this read fails outright.
	/// That was shipped briefly and broke 48 arms against real Vault, because Transit does not date every
	/// version it holds and this is the ordinary metadata path. Both halves are asserted here — the read
	/// succeeds, AND it invents nothing — because each one alone permits the other failure.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReportNoInstant_AndStillRead_WhenTheResponseCarriesNoneForThatVersion()
	{
		var provider = CreateProvider(TransitHoldingNoCreationTimes());

		var metadata = await provider.GetKeyVersionAsync(KeyId, 1, CancellationToken.None);

		metadata.ShouldNotBeNull(
			"an undated version is still a version: Transit does not date every key it holds, so refusing "
			+ "this read fails the ordinary metadata path for any such key");

		metadata!.CreatedAt.ShouldBeNull(
			"the instant is unknown, and unknown must be expressible. Any value here is a fabrication a "
			+ "caller cannot distinguish from a measurement, and a substituted clock sorts this version as "
			+ "the newest");

		metadata.Version.ShouldBe(1, "the rest of the metadata is unaffected by an absent instant");
	}

	/// <summary>
	/// LIVENESS. Without it every arm above is satisfied by a provider that refuses or returns null for
	/// everything, which would make the whole Vault key-metadata path unusable.
	/// </summary>
	[Fact]
	public async Task StillDescribeAVersionWhoseInstantIsPresent()
	{
		var provider = CreateProvider(TransitHolding(VersionOneCreated, VersionTwoCreated));

		var metadata = await provider.GetKeyVersionAsync(KeyId, 2, CancellationToken.None);

		metadata.ShouldNotBeNull();
		metadata!.Version.ShouldBe(2);
		metadata.KeyId.ShouldBe(KeyId);
	}

	/// <summary>A Transit response holding two versions with the given creation instants.</summary>
	private static EncryptionKeyInfo TransitHolding(DateTimeOffset first, DateTimeOffset second) => new()
	{
		LatestVersion = 2,
		Type = TransitKeyType.aes256_gcm96,
		DeletionAllowed = false,
		Keys = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["1"] = Version(first),
			["2"] = Version(second),
		},
	};

	/// <summary>A Transit response whose versions carry no creation time at all.</summary>
	private static EncryptionKeyInfo TransitHoldingNoCreationTimes() => new()
	{
		LatestVersion = 1,
		Type = TransitKeyType.aes256_gcm96,
		DeletionAllowed = false,
		Keys = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["1"] = new Dictionary<string, object>(StringComparer.Ordinal),
		},
	};

	// Transit reports the instant as an ISO-8601 string on the wire, so the fixture uses that shape rather
	// than a DateTimeOffset the mapping would accept without parsing.
	private static Dictionary<string, object> Version(DateTimeOffset createdAt) =>
		new(StringComparer.Ordinal)
		{
			["creation_time"] = createdAt.ToString("O", CultureInfo.InvariantCulture),
		};

	private static VaultKeyProvider CreateProvider(EncryptionKeyInfo keyInfo)
	{
		var provider = new VaultKeyProvider(
			Microsoft.Extensions.Options.Options.Create(new VaultOptions
			{
				VaultUri = new Uri("http://127.0.0.1:8200"),
				Keys = new() { KeyNamePrefix = "dispatch-" },
				Auth =
				{
					AuthMethod = VaultAuthMethod.Token,
					Token = "unit-token"
				}
			}),
			new MemoryCache(new MemoryCacheOptions()),
			NullLogger<VaultKeyProvider>.Instance);

		var transit = A.Fake<ITransitSecretsEngine>();
		_ = A.CallTo(() => transit.ReadEncryptionKeyAsync(A<string>._, A<string>._, A<string?>._))
			.Returns(Task.FromResult(new Secret<EncryptionKeyInfo> { Data = keyInfo }));

		var secrets = A.Fake<ISecretsEngine>();
		var v1 = A.Fake<IVaultClientV1>();
		_ = A.CallTo(() => v1.Secrets).Returns(secrets);
		_ = A.CallTo(() => secrets.Transit).Returns(transit);

		// The suspension marker lives in KV, and an unreachable mount must not be mistaken for "not
		// suspended" — so the read is answered as a plain absence rather than left to a dummy.
		var kv2 = A.Fake<VaultSharp.V1.SecretsEngines.KeyValue.V2.IKeyValueSecretsEngineV2>();
		var kv = A.Fake<VaultSharp.V1.SecretsEngines.KeyValue.IKeyValueSecretsEngine>();
		_ = A.CallTo(() => secrets.KeyValue).Returns(kv);
		_ = A.CallTo(() => kv.V2).Returns(kv2);
		A.CallTo(kv2)
			.Where(static call => call.Method.Name == "ReadSecretAsync")
			.WithReturnType<Task<Secret<SecretData>>>()
			.ReturnsLazily(static _ => throw new VaultApiException(HttpStatusCode.NotFound, "no such secret"));

		var client = new VaultClient(
			new VaultClientSettings("http://127.0.0.1:8200", new TokenAuthMethodInfo("unit-token")));

		SetPrivateField(client, "<V1>k__BackingField", v1);
		SetPrivateField(provider, "_vaultClient", client);

		return provider;
	}

	private static void SetPrivateField<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		field!.SetValue(instance, value);
	}
}
