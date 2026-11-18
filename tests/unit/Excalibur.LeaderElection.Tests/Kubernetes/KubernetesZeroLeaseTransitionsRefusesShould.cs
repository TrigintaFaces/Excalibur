// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using k8s.Autorest;
using k8s.Models;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Regression lock for the zero-mark admit-all in the Kubernetes fencing-token provider: a Lease that
/// EXISTS carrying <c>leaseTransitions: 0</c> must report NO token and refuse every token presented
/// against it.
/// </summary>
/// <remarks>
/// <para>
/// THE DEFECT THIS BINDS. The provider read <c>lease.Spec?.LeaseTransitions</c> and returned it verbatim,
/// so a Lease at zero -- the normal initial state, and the state of any Lease at that name created by
/// another controller -- answered the VALUE zero rather than "no mark". Validation is a high-water
/// comparison, <c>current.HasValue &amp;&amp; token &gt;= current.Value</c>, which against a mark of zero
/// evaluates <c>token &gt;= 0</c>: TRUE FOR EVERY NON-NEGATIVE TOKEN, including one a caller fabricated.
/// The state that must refuse everything admitted everything, and a delete-and-recreate of the Lease
/// returned the counter to zero and reset the fence permanently.
/// </para>
/// <para>
/// WHY BOTH FABRICATED VALUES ARE ASSERTED, and neither is redundant. Zero is the exact value the old code
/// admitted against a zero mark, so it is the arm that reddens on the defect itself. A large positive value
/// is the fabrication a hostile or confused caller actually supplies, and it is admitted by the same
/// comparison for the same reason. An implementation that special-cased only a presented zero would pass
/// one arm and still admit the other.
/// </para>
/// <para>
/// WHY THE LIVENESS ARMS ARE REQUIRED. Every safety arm here is satisfied by a provider that refuses
/// unconditionally and reports no token ever -- which is a worse bug than the one under repair, because it
/// bricks fencing for every consumer while looking fail-closed. The liveness arms prove the refusal is
/// specific to the unset mark: a positive count is still reported as the token, and a token at or above it
/// is still admitted.
/// </para>
/// <para>
/// WHAT THIS DOES NOT PROVE. The mark advancing with acquisition is a separate property and is not locked
/// here: this provider only READS the store's native counter and exposes no mint, so an unset mark
/// means "no transition has been recorded" even where the Lease already has a holder. A green fence here is
/// not a fenced resource.
/// </para>
/// <para>
/// A faked <c>IKubernetes</c> carries this faithfully, unlike the siblings that need real infrastructure:
/// the property under test is this provider's own handling of a value the client hands it, not any
/// server-side semantic of the API server. A fake returning a Lease at <c>leaseTransitions: 0</c> IS the
/// condition.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KubernetesZeroLeaseTransitionsRefusesShould
{
	private const string ResourceId = "orders-leader";

	/// <summary>The value the pre-fix comparison admitted against a mark of zero.</summary>
	private const long PresentedZeroToken = 0L;

	/// <summary>The fabrication a hostile caller supplies when it holds no real token.</summary>
	private const long FabricatedLargeToken = 9_000_000L;

	private readonly IKubernetes _client = A.Fake<IKubernetes>();
	private readonly ICoordinationV1Operations _coordination = A.Fake<ICoordinationV1Operations>();

	public KubernetesZeroLeaseTransitionsRefusesShould() =>
		A.CallTo(() => _client.CoordinationV1).Returns(_coordination);

	private KubernetesFencingTokenProvider CreateSut()
	{
		var options = Options.Create(new KubernetesLeaderElectionOptions
		{
			LeaseName = $"{ResourceId}-leader-election",
			Namespace = "default",
			InstanceId = "k8s-instance",
		});
		return new KubernetesFencingTokenProvider(_client, options);
	}

	/// <summary>
	/// Arranges a Lease that EXISTS -- the read succeeds, no 404 -- carrying the supplied transition count.
	/// A <see langword="null"/> count is the "field absent" shape the API server returns for a Lease that
	/// has never transitioned.
	/// </summary>
	private void LeaseExistsWithTransitions(int? transitions)
	{
		var lease = new V1Lease { Spec = new V1LeaseSpec { LeaseTransitions = transitions } };
		A.CallTo(() => _coordination.ReadNamespacedLeaseWithHttpMessagesAsync(
				A<string>._, A<string>._, A<bool?>._,
				A<IReadOnlyDictionary<string, IReadOnlyList<string>>>._, A<CancellationToken>._))
			.Returns(new HttpOperationResponse<V1Lease> { Body = lease });
	}

	[Theory]
	[InlineData(0)]
	[InlineData(null)]
	public async Task ReportNoTokenWhenTheLeaseRecordsNoTransition(int? transitions)
	{
		// SAFETY: zero and absent are the SAME state -- no transition recorded, therefore no token. A value
		// of zero here is what the fence then compares against, which is how the admit-all arose.
		LeaseExistsWithTransitions(transitions);
		var sut = CreateSut();

		var token = await sut.GetTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false);

		token.ShouldBeNull(
			"a Lease that exists but records no leadership transition carries no fencing token; reporting "
			+ "zero as a value is what makes a high-water comparison admit every non-negative token");
	}

	[Theory]
	[InlineData(0, PresentedZeroToken)]
	[InlineData(0, FabricatedLargeToken)]
	[InlineData(null, PresentedZeroToken)]
	[InlineData(null, FabricatedLargeToken)]
	public async Task RefuseEveryTokenWhenTheLeaseRecordsNoTransition(int? transitions, long presentedToken)
	{
		// SAFETY, and this is the arm the defect reddens: with no mark recorded, NOTHING validates --
		// neither the zero the old comparison admitted, nor the large value a caller fabricates.
		LeaseExistsWithTransitions(transitions);
		var sut = CreateSut();

		var isValid = await sut.ValidateTokenAsync(ResourceId, presentedToken, CancellationToken.None)
			.ConfigureAwait(false);

		isValid.ShouldBeFalse(
			$"a Lease recording no transition has no fencing mark, so the presented token {presentedToken} "
			+ "must be refused; the pre-fix provider reported the mark as zero and admitted every "
			+ "non-negative token against it");
	}

	[Fact]
	public async Task ExposeOnlyAReaderAndNoTokenWhenTheLeaseRecordsNoTransition()
	{
		LeaseExistsWithTransitions(0);
		IFencingTokenProvider sut = CreateSut();

		(sut is IFencingTokenIssuer).ShouldBeFalse();
		(await sut.GetTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
		(await sut.ValidateTokenAsync(ResourceId, 1, CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
	}

	[Fact]
	public async Task StillReportAPositiveTransitionCountAsTheToken()
	{
		// LIVENESS: the refusal is specific to an unset mark. A provider that reported no token ever would
		// satisfy every safety arm above while bricking fencing for every consumer.
		LeaseExistsWithTransitions(7);
		var sut = CreateSut();

		var token = await sut.GetTokenAsync(ResourceId, CancellationToken.None).ConfigureAwait(false);

		token.ShouldBe(7L, "a recorded transition count is the fencing token and must still be reported");
	}

	[Fact]
	public async Task StillAdmitATokenAtOrAboveARecordedMark()
	{
		// LIVENESS: the comparison still works where a mark exists -- a current leader's token validates,
		// and a stale leader's lower token does not. Without this arm a blanket refusal passes.
		LeaseExistsWithTransitions(7);
		var sut = CreateSut();

		var current = await sut.ValidateTokenAsync(ResourceId, 7L, CancellationToken.None).ConfigureAwait(false);
		var stale = await sut.ValidateTokenAsync(ResourceId, 6L, CancellationToken.None).ConfigureAwait(false);

		current.ShouldBeTrue("a token at the recorded high-water mark is the current leader's and must be admitted");
		stale.ShouldBeFalse("a token below the recorded high-water mark is a stale leader's and must be refused");
	}
}
