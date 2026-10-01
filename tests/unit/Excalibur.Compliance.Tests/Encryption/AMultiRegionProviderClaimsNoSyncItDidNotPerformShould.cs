// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// The multi-region decorator must not record a replication that did not happen, and must not hide the
/// capabilities of the provider it wraps.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first defect: a guard defeated by its only caller.</b> The sync routine deliberately records no
/// successful-sync instant — its own comment says so, because none of its branches copies key material, and
/// a recorded success is what would make a broken replication look like a working one. Three statements
/// after calling it, the caller stamped that instant anyway, zeroed the pending-key count and logged
/// replication complete. So the false success the callee refused to write was written for it. The
/// recovery-point check then computed its lag from a fabricated instant and reported the target met: a
/// consumer configuring disaster recovery saw a met recovery objective and an empty backlog over a passive
/// region holding no key material, and would learn otherwise at a failover.
/// </para>
/// <para>
/// <b>The second defect: a decorator that forwarded nothing.</b> Optional capabilities are discovered
/// through <c>GetService</c>, and this type never overrode it — so every capability it did not implement
/// itself resolved to <see langword="null"/> through it, including the durable-key capability the wrapped
/// cloud providers do supply. A multi-region deployment backed by a real key vault therefore reported as
/// having no durable key store at all, which the durability gate reads as VOLATILE for keys that are in
/// fact durable.
/// </para>
/// <para>
/// <b>Liveness matters in both.</b> "Never stamp a sync" is satisfied by a decorator that never replicates
/// anything, and "forward every capability" is satisfied by one that forwards its own away.
/// <see cref="StillAnswerForACapabilityItImplementsItself"/> and
/// <see cref="StillReportAPendingBacklogSoTheAbsenceIsVisible"/> are the arms that fail for those.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AMultiRegionProviderClaimsNoSyncItDidNotPerformShould
{
	/// <summary>
	/// SAFETY, and the RED arm. A replication that transferred no key material records no successful-sync
	/// instant.
	/// </summary>
	/// <remarks>
	/// RED input: any call to <c>ReplicateKeysAsync</c> at all, because no branch of the sync copies material
	/// today. Before the fix the instant was stamped unconditionally, so the very first replication made the
	/// recovery-point check report a met objective over a region that had received nothing.
	/// </remarks>
	[Fact]
	public async Task RecordNoSuccessfulSyncInstantWhenNoKeyMaterialMoved()
	{
		using var sut = CreateProvider();

		(await StatusOf(sut).ConfigureAwait(true)).LastSuccessfulSync
			.ShouldBeNull("nothing has replicated yet, so there is nothing to record.");

		await sut.ReplicateKeysAsync(keyId: null, TestContext.Current.CancellationToken).ConfigureAwait(true);

		(await StatusOf(sut).ConfigureAwait(true)).LastSuccessfulSync.ShouldBeNull(
			"no branch of the sync copies key material, so a successful-sync instant would be a success that "
			+ "never happened -- and the recovery-point check computes its lag from exactly this value.");
	}

	/// <summary>
	/// SAFETY. A replication of one named key is the same claim and is refused the same way.
	/// </summary>
	/// <remarks>
	/// The per-key path is the one an ordinary write takes on a synchronous-replication deployment, so a fix
	/// that only covered the replicate-everything path would leave the common case still lying.
	/// </remarks>
	[Fact]
	public async Task RecordNoSuccessfulSyncInstantForASingleKeyEither()
	{
		using var sut = CreateProvider();

		await sut.ReplicateKeysAsync("subject-key-1", TestContext.Current.CancellationToken).ConfigureAwait(true);

		(await StatusOf(sut).ConfigureAwait(true)).LastSuccessfulSync.ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS, and the arm that stops the two above being satisfied by silence. The pending-key backlog is
	/// still visible after a replication that moved nothing.
	/// </summary>
	/// <remarks>
	/// Zeroing the backlog was the same false claim in a second field: a consumer reading an empty backlog
	/// concludes there is nothing outstanding. A decorator that reported no backlog AND no sync instant would
	/// satisfy the safety arms while telling the consumer everything is fine.
	/// </remarks>
	[Fact]
	public async Task StillReportAPendingBacklogSoTheAbsenceIsVisible()
	{
		using var sut = CreateProvider();

		// An ordinary provisioning on an asynchronous-replication deployment defers the copy, which is what
		// puts a key in the backlog.
		_ = await sut.CreateKeyIfAbsentAsync(
			"subject-key-1", EncryptionAlgorithm.Aes256Gcm, purpose: null, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		var status = await StatusOf(sut).ConfigureAwait(true);

		status.PendingKeys.ShouldBeGreaterThan(
			0,
			"a key whose material was never copied to the passive region is outstanding, and reporting an "
			+ "empty backlog tells the consumer the opposite.");
	}

	/// <summary>
	/// SAFETY. A capability the wrapped provider supplies and this type does not is reachable through it.
	/// </summary>
	/// <remarks>
	/// RED input: asking for <see cref="IDurableKeyProvider"/>, which this type does not implement and the
	/// wrapped provider does. Before the fix the decorator returned <see langword="null"/> for it, so a
	/// cloud-backed multi-region deployment read as a volatile key store.
	/// </remarks>
	[Fact]
	public void ForwardACapabilityOnlyTheWrappedProviderSupplies()
	{
		var primary = A.Fake<IKeyManagementProvider>(o => o
			.Implements<IKeyManagementAdmin>()
			.Implements<IDurableKeyProvider>());

		// A fake answers EVERY GetService call with a non-null proxy unless told otherwise, which would make
		// the absence arm pass for the wrong reason and this arm pass without forwarding anything. Default to
		// null first, then supply only the capability this region genuinely has.
		A.CallTo(() => primary.GetService(A<Type>._)).Returns(null);
		A.CallTo(() => primary.GetService(typeof(IDurableKeyProvider))).Returns(primary);

		using var sut = CreateProvider(primary);

		sut.GetService(typeof(IDurableKeyProvider)).ShouldNotBeNull(
			"the wrapped provider supplies this capability; a decorator that does not forward it silently "
			+ "disables it, and a durable key store then reads as volatile.");
	}

	/// <summary>
	/// SAFETY twin. This type's OWN implementation wins over the wrapped provider's.
	/// </summary>
	/// <remarks>
	/// The decorator's destruction-status answer spans both regions; the wrapped provider's describes one of
	/// them. Forwarding blindly would return the narrower answer, and a key still live in the passive region
	/// would read as destroyed.
	/// </remarks>
	[Fact]
	public void StillAnswerForACapabilityItImplementsItself()
	{
		using var sut = CreateProvider();

		sut.GetService(typeof(IKeyDestructionStatusProvider)).ShouldBeSameAs(
			sut,
			"this type implements the capability across both regions, so its own answer is the correct one.");
	}

	/// <summary>
	/// SAFETY. A capability nobody supplies is still absent, so forwarding did not become inventing.
	/// </summary>
	[Fact]
	public void StillReportACapabilityNobodySuppliesAsAbsent()
	{
		using var sut = CreateProvider();

		sut.GetService(typeof(IKeyWrappingProvider)).ShouldBeNull();
	}

	private static Task<ReplicationStatus> StatusOf(MultiRegionKeyProvider sut) =>
		sut.GetReplicationStatusAsync(TestContext.Current.CancellationToken);

	private static MultiRegionKeyProvider CreateProvider(IKeyManagementProvider? primary = null)
	{
		var active = primary ?? Region();

		return new MultiRegionKeyProvider(
			active,
			Region(),
			Options(),
			NullLogger<MultiRegionKeyProvider>.Instance);
	}

	private static IKeyManagementProvider Region()
	{
		var region = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyManagementAdmin>());

		// Supplies no optional capability. Stated explicitly because a fake otherwise returns a non-null proxy
		// for every capability asked of it -- which would make a decorator that forwards nothing look correct.
		A.CallTo(() => region.GetService(A<Type>._)).Returns(null);

		A.CallTo(() => ((IKeyManagementAdmin)region).ListKeysAsync(
				A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		A.CallTo(() => region.CreateKeyIfAbsentAsync(
				A<string>._, A<EncryptionAlgorithm>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new KeyMetadata
			{
				KeyId = "subject-key-1",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UnixEpoch,
			}));

		return region;
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
}
