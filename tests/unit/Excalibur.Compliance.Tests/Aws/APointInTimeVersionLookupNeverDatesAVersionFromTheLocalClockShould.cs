// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Excalibur.Compliance;
using Excalibur.Compliance.Aws;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using KmsKeyMetadata = Amazon.KeyManagementService.Model.KeyMetadata;

namespace Excalibur.Compliance.Tests.Aws;

/// <summary>
/// <see cref="AwsKmsHistoricalKeyProvider"/> selects the key version that was live at a requested instant by
/// comparing each version's <see cref="KeyMetadata.CreatedAt"/>. A version whose instant the backend did not
/// supply must therefore make the lookup FAIL, never be dated from the local clock.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS IS THE SEAM WHERE A FABRICATED INSTANT IS NOT COSMETIC.</b> The selection walks the versions in
/// creation order, keeps the last one that predates the requested instant, and <c>break</c>s at the first one
/// that postdates it. A version stamped with <c>UtcNow</c> appears to postdate every historical request — so
/// it does not merely sort oddly, it TRUNCATES THE SCAN. The lookup then returns an earlier version than the
/// one that was actually live, or none, and reports success either way.
/// </para>
/// <para>
/// <b>Why that matters more than the wrong date.</b> Returning the wrong version from a point-in-time lookup
/// is a silent failure: the caller receives well-formed metadata for a real version and has nothing to
/// compare it against. The fabrication was in the only provider in the tree that orders key VERSIONS by this
/// field, which is the combination that turns a latent trap into a live one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class APointInTimeVersionLookupNeverDatesAVersionFromTheLocalClockShould
{
	private const string KeyId = "orders";

	private static readonly DateTime KeyCreated = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	private static readonly DateTime RotatedOn = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

	/// <summary>
	/// THE LOAD-BEARING ARM. A rotation the backend did not date makes the lookup FAIL rather than quietly
	/// resolve to the previous version.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: date the undated rotation from the local clock and this lookup returns
	/// version 1 and reports success — because the fabricated instant postdates the requested one, the scan
	/// stops there, and version 1 is the last version it kept. A caller asking which key was live in
	/// September receives January's.
	/// </remarks>
	[Fact]
	public async Task Fail_RatherThanResolveToAnEarlierVersion_WhenARotationHasNoDate()
	{
		var provider = CreateProvider(Kms(keyCreated: KeyCreated, rotations: [null]));

		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.GetKeyVersionAsync(
				KeyId, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None));

		refusal.Message.ShouldContain(
			"refused",
			Case.Insensitive,
			"the refusal must say it declined to date the version, so this is not read as a missing key");
	}

	/// <summary>
	/// The same refusal for the key's own creation instant, which dates version 1.
	/// </summary>
	[Fact]
	public async Task Fail_WhenTheKeyItselfHasNoCreationDate()
	{
		var provider = CreateProvider(Kms(keyCreated: null, rotations: []));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => provider.GetKeyVersionAsync(
				KeyId, DateTimeOffset.UtcNow, CancellationToken.None));
	}

	/// <summary>
	/// LIVENESS, and the property the arms above protect. With every instant supplied, a lookup between the
	/// two dates resolves to the version that was live then — version 1, not the rotation that came later.
	/// </summary>
	/// <remarks>
	/// Without this arm, both refusals above are satisfied by a provider that fails every lookup, which is
	/// the cheapest way to never return a wrong version and would make the whole point-in-time path useless.
	/// </remarks>
	[Fact]
	public async Task StillResolveTheVersionThatWasLiveAtTheRequestedInstant()
	{
		var provider = CreateProvider(Kms(keyCreated: KeyCreated, rotations: [RotatedOn]));

		var live = await provider.GetKeyVersionAsync(
			KeyId, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		live.ShouldNotBeNull();
		live!.Version.ShouldBe(
			1,
			"March falls after the key's creation in January and before the rotation in June, so version 1 was "
			+ "the version live at that instant");
	}

	/// <summary>
	/// LIVENESS — the other side of the same selection, so the arm above is not satisfied by a provider that
	/// always answers version 1.
	/// </summary>
	[Fact]
	public async Task ResolveTheRotatedVersionForAnInstantAfterTheRotation()
	{
		var provider = CreateProvider(Kms(keyCreated: KeyCreated, rotations: [RotatedOn]));

		var live = await provider.GetKeyVersionAsync(
			KeyId, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

		live.ShouldNotBeNull();
		live!.Version.ShouldBe(2, "September falls after the June rotation");
	}

	/// <summary>
	/// A KMS client describing one key with the given creation date and rotation dates. A <see langword="null"/>
	/// entry is a rotation the backend reported without a date — the shape the refusal exists for.
	/// </summary>
	private static IAmazonKeyManagementService Kms(DateTime? keyCreated, IReadOnlyList<DateTime?> rotations)
	{
		var kms = A.Fake<IAmazonKeyManagementService>();

		_ = A.CallTo(() => kms.DescribeKeyAsync(A<DescribeKeyRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DescribeKeyResponse
			{
				KeyMetadata = new KmsKeyMetadata
				{
					KeyId = "11111111-2222-3333-4444-555555555555",
					CreationDate = keyCreated,
				},
			}));

		_ = A.CallTo(() => kms.ListKeyRotationsAsync(A<ListKeyRotationsRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new ListKeyRotationsResponse
			{
				Rotations = [.. rotations.Select(static date => new RotationsListEntry { RotationDate = date })],
			}));

		return kms;
	}

	private static AwsKmsHistoricalKeyProvider CreateProvider(IAmazonKeyManagementService kms) =>
		new(
			kms,
			Options.Create(new AwsKmsHistoricalKeyOptions { CacheKeyVersions = false }),
			Options.Create(new AwsKmsOptions()),
			NullLogger<AwsKmsHistoricalKeyProvider>.Instance);
}
