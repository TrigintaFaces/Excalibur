// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Kubernetes;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.Options;

using Excalibur.Testing.Conformance;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Runs the fencing-contract conformance arms against the Kubernetes provider on a real API server.
/// </summary>
/// <remarks>
/// <para>
/// ZERO IS UNSET, AND THAT IS WHAT THESE ARMS ASSERT. A Lease carrying a <c>leaseTransitions</c> of zero,
/// and a Lease that does not exist, are the same state: no transition has been recorded, so no token was
/// ever issued. The provider answers <see langword="null"/> for both and refuses every token against them.
/// Reporting zero as a value instead made the high-water comparison read <c>token &gt;= 0</c>, which is true
/// for every non-negative token including a fabricated one — the state that must refuse everything admitted
/// everything, and a delete-and-recreate of the Lease reset the fence permanently.
/// </para>
/// <para>
/// THIS PROVIDER DOES NOT MINT, which is why <see cref="EstablishTokenAsync"/> is overridden. It reads the
/// Lease's own <c>leaseTransitions</c>, a counter the election advances on a genuine handover of the holder
/// identity. It exposes no <see cref="IFencingTokenIssuer"/>; the override drives real native
/// transitions. The base's default establish requires mint capability and cannot be used here.
/// </para>
/// <para>
/// THE MARK IS ADVANCED BY WRITING THE LEASE, NOT BY DRIVING AN ELECTION, and that is deliberate. The
/// subject of these arms is the provider's read-and-validate behaviour against a given substrate state;
/// driving <c>KubernetesLeaderElection</c> would make the subject two components at once and would import
/// its lease-duration timing into every arm. That the election advances the counter correctly on a real
/// handover is a different contract, covered against this same k3s fixture by
/// <see cref="KubernetesFencingConcurrentAcquisitionShould"/>. Writing the substrate directly is the same
/// approach the sequence-backed providers use for their unadvanced-mark arm, and for the same reason: the
/// provider offers no API for a state it is only ever asked to read.
/// </para>
/// <para>
/// NON-VACUITY, the part that is easy to get silently wrong here: <see cref="EstablishTokenAsync"/> returns
/// the value it WROTE to the Lease, never the value it reads back through the provider. Returning the
/// provider's own read would make <c>GetTokenAsync_ReturnsTheEstablishedToken</c> compare a read against
/// itself, and that arm would pass for a provider that returned any constant.
/// </para>
/// <para>
/// The concurrency arm is gated off through <see cref="ProviderMintsItsOwnTokens"/>: there is no mint of
/// this provider's to race, and racing concurrent Lease writes would measure the API server's own
/// compare-and-swap rather than anything here. The substrate's transition mechanism is raced against real
/// k3s by <see cref="KubernetesFencingConcurrentAcquisitionShould"/>, which is the arm the base's gate says
/// a read-native suite owes.
/// </para>
/// <para>
/// THE ARM THAT ACTUALLY CATCHES THIS PROVIDER'S DEFECT IS THE UNADVANCED-MARK ONE, not the two
/// never-issued arms, and the difference is measurable. A fresh resource id leaves no Lease at all, and an
/// absent Lease is answered as no-token by the not-found path regardless of how the counter is interpreted —
/// so with only those arms, reporting a <c>leaseTransitions</c> of zero as a real mark stays GREEN. The
/// reachable state is the one the provider's own read path names: a Lease that EXISTS carrying a counter of
/// zero, which is the normal initial state and the state of any Lease at this name created by another
/// controller. <see cref="CreateUnadvancedMarkAsync"/> constructs it, and that is the arm the shipped
/// defect reddens.
/// </para>
/// </remarks>
[Collection(KubernetesLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Conformance")]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Infrastructure", "Kubernetes")]
public sealed class KubernetesFencingTokenProviderConformanceShould : FencingTokenProviderConformanceTestKit, IAsyncLifetime
{
	/// <inheritdoc/>
	protected override CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

	[Fact]
	public override Task GetTokenAsync_ReturnsNull_WhenNoTokenWasEverIssued() => base.GetTokenAsync_ReturnsNull_WhenNoTokenWasEverIssued();

	[Theory]
	[InlineData(long.MinValue)]
	[InlineData(-1L)]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(2L)]
	[InlineData(long.MaxValue)]
	public override Task ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued(long presented) => base.ValidateTokenAsync_RefusesEveryToken_WhenNoTokenWasEverIssued(presented);

	[Fact]
	public override Task UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken() => base.UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken();

	[Fact]
	public override Task EstablishedToken_IsStrictlyPositive() => base.EstablishedToken_IsStrictlyPositive();

	[Fact]
	public override Task GetTokenAsync_ReturnsTheEstablishedToken() => base.GetTokenAsync_ReturnsTheEstablishedToken();

	[Fact]
	public override Task EstablishToken_IsStrictlyMonotonicAcrossHandovers() => base.EstablishToken_IsStrictlyMonotonicAcrossHandovers();

	[Fact]
	public override Task ValidateTokenAsync_AdmitsThePinnedIncumbentToken() => base.ValidateTokenAsync_AdmitsThePinnedIncumbentToken();

	[Fact]
	public override Task ValidateTokenAsync_RefusesTheSupersededToken_AfterAHandover() => base.ValidateTokenAsync_RefusesTheSupersededToken_AfterAHandover();

	[Fact]
	public override Task ConcurrentEstablishes_YieldDistinctTokens() => base.ConcurrentEstablishes_YieldDistinctTokens();

	[Fact]
	public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

	private const string Namespace = "default";

	private readonly KubernetesContainerFixture _fixture;

	/// <summary>
	/// Every Lease this suite created, so teardown removes exactly those and nothing else.
	/// </summary>
	/// <remarks>
	/// Recorded rather than derived from a name pattern: the resource ids come from the base, one per arm,
	/// and this type is the only thing that writes a Lease for them. Deleting by pattern would risk removing
	/// a Lease another suite on this shared fixture owns.
	/// </remarks>


	private IKubernetes? _client;
    private readonly KubernetesElectionTestLifetime _ownership = new();
	private ServiceProvider? _services;

	public KubernetesFencingTokenProviderConformanceShould(KubernetesContainerFixture fixture) =>
		_fixture = fixture;

	/// <inheritdoc/>
	protected override bool ProviderMintsItsOwnTokens => false;

	/// <inheritdoc/>
	/// <remarks>
	/// A Lease exists before any transition is recorded against it. Its <c>leaseTransitions</c> is zero on
	/// creation, and a Lease at this name created by some other controller carries zero or nothing at all —
	/// both reachable, neither involving this framework. So the substrate does have a mark-bearing artifact
	/// that exists while carrying no value, and it is the state the shipped inversion mishandled.
	/// </remarks>
	protected override bool StoreCanHoldAnUnadvancedMark => true;

	/// <inheritdoc/>
	protected override async Task<IFencingTokenProvider> CreateProviderAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fencing contract is a split-brain safety control, so its conformance arms must never be "
			+ "skipped for want of a k3s API server");

		_client = _ownership.OwnClient(await _fixture.CreateClientAsync().ConfigureAwait(false));

		var services = new ServiceCollection();
		_ = services.AddSingleton(_client);

		// LeaseName is deliberately left unset so the provider derives one per resource id. Setting it
		// would point every arm's resource at a single Lease, and the arms that assert the never-issued
		// state would then read a mark some earlier arm established.
		_ = services.AddSingleton(Options.Create(new KubernetesLeaderElectionOptions { Namespace = Namespace }));
		_ = services.AddKubernetesFencingTokenProvider();
		_services = services.BuildServiceProvider();
        _ownership.OwnServices(_services);

		return _services.GetRequiredService<IFencingTokenProvider>();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// Deletes the Leases this arm created. The k3s fixture is shared across a collection, so a Lease left
	/// behind outlives the arm that made it and becomes shared state on the namespace every later arm reads.
	/// It is harmless to these arms — the base takes a fresh resource id per arm, so a stale Lease is at a
	/// name nothing will ask for again — but it stops being harmless as soon as a suite on this fixture uses
	/// a resource name it does not randomise, at which point a takeover or mutual-exclusion arm can pass
	/// against a predecessor's leftovers rather than against the election under test.
	/// </para>
	/// <para>
	/// A Lease already gone is the expected case and is tolerated. Any other failure is allowed to
	/// propagate: an API server that cannot delete will fail the next arm anyway, so a loud cleanup failure
	/// costs nothing that was not already lost, while swallowing it would reintroduce silent accumulation —
	/// the thing this exists to prevent.
	/// </para>
	/// </remarks>
    protected override Task CleanupAsync() => _ownership.DisposeAsync().AsTask();

	/// <inheritdoc/>
	/// <remarks>
	/// Advances the Lease's <c>leaseTransitions</c> by one, which is what one leadership transition does to
	/// this substrate, and returns the value written.
	/// </remarks>
	protected override async Task<long> EstablishTokenAsync(string resourceId)
	{
		var client = _client ?? throw new InvalidOperationException("The provider was not created.");
		var leaseName = LeaseNameFor(resourceId);
        _ownership.RecordLease(client, leaseName, Namespace);
		var cancellationToken = TestContext.Current.CancellationToken;

		var existing = await ReadLeaseAsync(client, leaseName, cancellationToken).ConfigureAwait(false);

		if (existing is null)
		{
			// The first transition. One, not zero: zero is the unset state these arms assert is refused.
			var created = new V1Lease
			{
				Metadata = new V1ObjectMeta { Name = leaseName, NamespaceProperty = Namespace },
				Spec = new V1LeaseSpec
				{
					HolderIdentity = "fencing-conformance",
					LeaseTransitions = 1,
					AcquireTime = DateTime.UtcNow,
					RenewTime = DateTime.UtcNow,
					LeaseDurationSeconds = 15,
				},
			};

			_ = await client.CoordinationV1
				.CreateNamespacedLeaseAsync(created, Namespace, cancellationToken: cancellationToken)
				.ConfigureAwait(false);



			return 1L;
		}

		var next = (existing.Spec?.LeaseTransitions ?? 0) + 1;

		existing.Spec ??= new V1LeaseSpec();
		existing.Spec.LeaseTransitions = next;
		existing.Spec.HolderIdentity = "fencing-conformance-" + next.ToString(System.Globalization.CultureInfo.InvariantCulture);
		existing.Spec.RenewTime = DateTime.UtcNow;

		_ = await client.CoordinationV1
			.ReplaceNamespacedLeaseAsync(existing, leaseName, Namespace, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return next;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Creates the Lease with a transition count of zero: it exists, it is readable, and no transition has
	/// ever been recorded against it. An explicit zero rather than an omitted field so the arm is
	/// deterministic about which of the two forms it presents; the provider's read collapses both to no-token
	/// through the same comparison, and an absent counter is the weaker case because it cannot be mistaken
	/// for a value at all.
	/// </remarks>
	protected override async Task CreateUnadvancedMarkAsync(string resourceId)
	{
		var client = _client ?? throw new InvalidOperationException("The provider was not created.");
        _ownership.RecordLease(client, LeaseNameFor(resourceId), Namespace);

		var lease = new V1Lease
		{
			Metadata = new V1ObjectMeta { Name = LeaseNameFor(resourceId), NamespaceProperty = Namespace },
			Spec = new V1LeaseSpec
			{
				HolderIdentity = "fencing-conformance-unadvanced",
				LeaseTransitions = 0,
				AcquireTime = DateTime.UtcNow,
				RenewTime = DateTime.UtcNow,
				LeaseDurationSeconds = 15,
			},
		};

		_ = await client.CoordinationV1
			.CreateNamespacedLeaseAsync(lease, Namespace, cancellationToken: TestContext.Current.CancellationToken)
			.ConfigureAwait(false);


	}

	/// <summary>
	/// Derives the Lease name the provider derives, so the arms read the Lease the provider reads.
	/// </summary>
	/// <param name="resourceId"> The resource whose Lease to name. </param>
	/// <returns> The Lease name. </returns>
	/// <remarks>
	/// This mirrors the provider's own default for an unset <c>LeaseName</c>. A different name would make
	/// every arm read a Lease nothing under test ever writes, so each would pass against a provider that
	/// answered null unconditionally.
	/// </remarks>
	private static string LeaseNameFor(string resourceId) => $"{resourceId}-leader-election";

	private static async Task<V1Lease?> ReadLeaseAsync(
		IKubernetes client,
		string leaseName,
		CancellationToken cancellationToken)
	{
		try
		{
			return await client.CoordinationV1
				.ReadNamespacedLeaseAsync(leaseName, Namespace, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}
}
