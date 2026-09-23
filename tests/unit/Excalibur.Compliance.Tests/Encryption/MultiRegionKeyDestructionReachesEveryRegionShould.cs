// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the crypto-shredding guarantee across regions: a key is destroyed only when it is unreadable in
/// EVERY region that held it.
/// <para>
/// Destroying the key in the active region alone produces a certificate that is TRUE WHEN ISSUED and
/// FALSIFIED LATER by a routine, designed event — <see cref="MultiRegionKeyProvider"/> flips its active
/// provider on automatic failover, at which point the surviving secondary copy becomes readable again and
/// the data subject's erasure silently reverts. That is an attestation with an expiry nobody tracks, which
/// is why the destruction must reach every region rather than propagate by assumption.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionKeyDestructionReachesEveryRegionShould : IDisposable
{
	private const string KeyId = "subject-key-1";

	private readonly IKeyManagementProvider _primary = RegionThatReportsDestruction();
	private readonly IKeyManagementProvider _secondary = RegionThatReportsDestruction();
	private readonly MultiRegionKeyProvider _sut;

	public MultiRegionKeyDestructionReachesEveryRegionShould()
	{
		A.CallTo(() => ((IKeyManagementAdmin)_primary).ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));
		A.CallTo(() => ((IKeyManagementAdmin)_secondary).ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		_sut = new MultiRegionKeyProvider(_primary, _secondary, Options(), NullLogger<MultiRegionKeyProvider>.Instance);
	}

	private static MultiRegionOptions Options() =>
		new()
		{
			Primary = new RegionConfiguration
			{
				RegionId = "us-east-1",
				Endpoint = new Uri("https://primary.example.com"),
			},
			Secondary = new RegionConfiguration
			{
				RegionId = "us-west-2",
				Endpoint = new Uri("https://secondary.example.com"),
			},
			Failover =
			{
				HealthCheckInterval = TimeSpan.FromHours(1),
				EnableAutomaticFailover = false,
			},
		};

	// THE ARM THE DEFECT NAMES: the key must be destroyed in BOTH regions, not just the active one.
	[Fact]
	public async Task DestroyTheKeyInTheSecondaryRegionAndNotOnlyTheActiveOne()
	{
		GivenDestruction(_primary, KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
		GivenDestruction(_secondary, KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));

		var outcome = await _sut.DeleteKeyAsync(KeyId, 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.Completed);

		// The secondary is the region a failover promotes. If destruction never reaches it, the erasure is
		// undone the moment the provider flips -- so this assertion is the whole guarantee.
		A.CallTo(() => ((IKeyManagementAdmin)_secondary).DeleteKeyAsync(KeyId, 0, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => ((IKeyManagementAdmin)_primary).DeleteKeyAsync(KeyId, 0, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	// A region that only SCHEDULED destruction leaves the key recoverable until it purges, so the combined
	// outcome must be the weakest one -- never Completed.
	[Fact]
	public async Task RefuseToReportCompletedWhenOneRegionOnlyScheduledDestruction()
	{
		var purgeAt = DateTimeOffset.UtcNow.AddDays(7);
		GivenDestruction(_primary, KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
		GivenDestruction(_secondary, KeyDestructionOutcome.ScheduledAt(purgeAt));

		var outcome = await _sut.DeleteKeyAsync(KeyId, 0, CancellationToken.None);

		outcome.State.ShouldBe(
			KeyDestructionState.ScheduledIrreversible,
			"the key stays readable in the scheduled region until it purges, so the destruction is not complete");
		outcome.IrreversibleAt.ShouldBe(purgeAt, "the key is recoverable until the LAST region purges");
		outcome.IsIrreversibleNow.ShouldBeFalse();
	}

	// A region that FAILS outright leaves the key possibly readable there. There is no value in the
	// tri-state meaning "destroyed here, unknown there", so this must not return a state at all.
	[Fact]
	public async Task RefuseToReportAnyDestructionStateWhenARegionFails()
	{
		GivenDestruction(_primary, KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
		A.CallTo(() => ((IKeyManagementAdmin)_secondary).DeleteKeyAsync(KeyId, 0, A<CancellationToken>._))
			.Throws(new TimeoutException("secondary region unreachable"));

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await _sut.DeleteKeyAsync(KeyId, 0, CancellationToken.None));

		// The caller records a throw as an error, which is what keeps a completion attestation unreachable.
		thrown.Message.ShouldContain("us-west-2");
		thrown.Message.ShouldContain("PARTIAL");
	}

	// LIVENESS CONTROL: the ordinary idempotent no-op must still be reachable. Without this arm, a
	// DeleteKeyAsync that refused everything -- destroying nothing, ever -- would satisfy every arm above.
	[Fact]
	public async Task StillReportNotFoundWhenNoRegionHeldTheKey()
	{
		GivenDestruction(_primary, KeyDestructionOutcome.NotFound);
		GivenDestruction(_secondary, KeyDestructionOutcome.NotFound);

		var outcome = await _sut.DeleteKeyAsync(KeyId, 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.NotFound);
	}

	// LIVENESS CONTROL, the one the bead names: an ordinary ROTATE must still replicate. The destruction
	// fix must not be achieved by disabling the replication path it sits beside.
	[Fact]
	public async Task StillReplicateOnAnOrdinaryRotate()
	{
		A.CallTo(() => _primary.RotateKeyAsync(KeyId, A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new KeyRotationResult { Success = true }));

		var result = await _sut.RotateKeyAsync(
			KeyId, EncryptionAlgorithm.Aes256Gcm, purpose: null, expiresAt: null, CancellationToken.None);

		result.Success.ShouldBeTrue("rotation must keep working; the destruction fix must not disable it");
		A.CallTo(() => _primary.RotateKeyAsync(KeyId, A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	// Confirmation must ask EVERY region. GetKeyAsync asks only the active one, so a key the secondary still
	// holds would be confirmed destroyed and the erasure completed over a recoverable copy.
	[Fact]
	public async Task NotReportTheKeyDestroyedWhileTheSecondaryRegionStillHoldsIt()
	{
		GivenDestroyed(_primary, true);
		GivenDestroyed(_secondary, false);

		(await _sut.IsKeyDestroyedAsync(KeyId, CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task ReportTheKeyDestroyedOnlyWhenEveryRegionHasDestroyedIt()
	{
		GivenDestroyed(_primary, true);
		GivenDestroyed(_secondary, true);

		(await _sut.IsKeyDestroyedAsync(KeyId, CancellationToken.None)).ShouldBeTrue();
	}

	// A region whose provider cannot answer authoritatively is never counted as destroyed -- its lookup's
	// "not found" is also what a backend with a recovery window says about a key it can still restore -- even
	// when every other region reports the key destroyed.
	[Fact]
	public async Task NotReportTheKeyDestroyedWhenOneRegionCannotConfirmDestructionEvenIfItsLookupFindsNothing()
	{
		var cannotConfirm = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyManagementAdmin>());
		A.CallTo(() => ((IKeyManagementAdmin)cannotConfirm).ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));
		A.CallTo(() => cannotConfirm.GetKeyAsync(KeyId, A<CancellationToken>._)).Returns(Task.FromResult<KeyMetadata?>(null));
		GivenDestroyed(_primary, true);
		using var sut = new MultiRegionKeyProvider(_primary, cannotConfirm, Options(), NullLogger<MultiRegionKeyProvider>.Instance);

		(await sut.IsKeyDestroyedAsync(KeyId, CancellationToken.None)).ShouldBeFalse();
	}

	public void Dispose() => _sut.Dispose();

	private static IKeyManagementProvider RegionThatReportsDestruction()
	{
		var region = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyManagementAdmin>().Implements<IKeyDestructionStatusProvider>());
		A.CallTo(() => region.GetService(typeof(IKeyDestructionStatusProvider))).Returns(region);
		return region;
	}

	private static void GivenDestroyed(IKeyManagementProvider region, bool destroyed) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)region).IsKeyDestroyedAsync(KeyId, A<CancellationToken>._))
			.Returns(Task.FromResult(destroyed));

	private static void GivenDestruction(IKeyManagementProvider provider, KeyDestructionOutcome outcome) =>
		A.CallTo(() => ((IKeyManagementAdmin)provider).DeleteKeyAsync(KeyId, 0, A<CancellationToken>._))
			.Returns(Task.FromResult(outcome));
}
