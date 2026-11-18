// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A lapsed execution claim must reach an executor that ACTS on it, not merely one that is offered it.
/// </summary>
/// <remarks>
/// <para>
/// The lease made an interrupted erasure re-CLAIMABLE. The discovery predicate made it re-OFFERED. Both
/// landed, both were RED-proven, and the request was still never erased: the executor's entry guard
/// admitted only the scheduled status, so a recovered request was handed to a consumer that refused it on
/// every poll. Offered forever, executed never, keys possibly already destroyed, nothing reporting it.
/// </para>
/// <para>
/// <b>That is the same defect as the one the lease was built to fix, surviving one level above the
/// lease</b>, and it is why neither of the earlier arms could see it: each bound its own half correctly.
/// The query was asked whether it offers the request. Nobody asked what happens to the request it
/// offers. This arm binds the whole path, from a stranded row to a completed erasure.
/// </para>
/// <para>
/// <b>Both halves, because each alone is satisfied by a broken executor.</b> An executor that refuses
/// every in-progress request satisfies the safety arm and strands every interrupted erasure. One that
/// accepts every in-progress request satisfies the liveness arm and re-executes a run that is healthy
/// and in flight, which is two passes erasing one subject.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class ALapsedErasureIsActuallyReexecutedShould
{
	/// <summary>
	/// THE LIVENESS HALF: a request stranded in the claimed status with a lapsed lease is executed to
	/// completion by the next pass.
	/// </summary>
	/// <remarks>
	/// RED before the entry guard admitted the claimed status: the executor answered
	/// "Invalid status: InProgress" and the request stayed exactly as the dead executor left it.
	/// </remarks>
	[Fact]
	public async Task Execute_a_request_stranded_in_progress_whose_claim_has_lapsed()
	{
		using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var harness = new ErasureLifecycleHarness(keys, keys);

		var requestId = await SubmitAsync(harness, "subject-stranded").ConfigureAwait(true);

		// A zero lease is legal and yields a claim that has ALREADY lapsed, which is the only
		// provider-agnostic way to construct the state a dead executor leaves behind without controlling a
		// clock. This is the executor that died: it took custody and never came back.
		var abandoned = await harness.Store.ClaimForExecutionAsync(
				requestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(true);

		abandoned.Outcome.ShouldBe(
			ErasureClaimOutcome.Granted,
			"the fixture's own precondition: without a granted claim the request is never in the stranded "
			+ "state this arm exists to test, and the arm would pass against an executor that recovers "
			+ "nothing");

		(await harness.StatusOfAsync(requestId).ConfigureAwait(true)).ShouldBe(
			ErasureRequestStatus.InProgress,
			"taking custody from the scheduled status advances it, which is what makes the request "
			+ "invisible to every query that looks for work by status alone");

		// The scheduler's next poll. The discovery predicate offers this request; the question this arm
		// asks is what the executor does with it.
		var recovered = await harness.Service.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(true);

		recovered.Success.ShouldBeTrue(
			"an erasure whose executor died must be performed by a later pass. The lease makes it "
			+ "re-claimable and the discovery predicate offers it; if the executor then refuses it, the "
			+ "subject's data is never erased and nothing in the system reports that. Outcome "
			+ $"{recovered.Outcome}, message: {recovered.ErrorMessage}");

		(await harness.StatusOfAsync(requestId).ConfigureAwait(true)).ShouldBe(
			ErasureRequestStatus.Completed,
			"the recovered pass must carry the request to a terminal, certified status rather than leaving "
			+ "it claimed again");
	}

	/// <summary>
	/// THE SAFETY HALF: a request whose lease is still LIVE is not re-executed, because its executor is
	/// still working.
	/// </summary>
	/// <remarks>
	/// The decision is the STORE'S, not this guard's: the status was read a moment earlier and carries no
	/// custody, so testing it here would be a read followed by a write with a window between them. The
	/// store tests custody before status in one indivisible step and answers ALREADY CLAIMED, which is
	/// what makes a healthy in-flight run safe from the recovery path above.
	/// </remarks>
	[Fact]
	public async Task Refuse_a_request_in_progress_whose_claim_is_still_live()
	{
		using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var harness = new ErasureLifecycleHarness(keys, keys);

		var requestId = await SubmitAsync(harness, "subject-in-flight").ConfigureAwait(true);

		var holder = await harness.Store.ClaimForExecutionAsync(
				requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(true);

		holder.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var refused = await harness.Service.ExecuteAsync(requestId, CancellationToken.None)
			.ConfigureAwait(true);

		refused.Success.ShouldBeFalse(
			"a request another instance is actively erasing must not be executed a second time. Two "
			+ "concurrent passes each destroy the same subject's keys and each write a completion "
			+ "certificate, one of them attesting coverage its own pass did not establish");

		(await harness.StatusOfAsync(requestId).ConfigureAwait(true)).ShouldBe(
			ErasureRequestStatus.InProgress,
			"the refused pass must leave the holder's request exactly as it found it");
	}

	private static async Task<Guid> SubmitAsync(ErasureLifecycleHarness harness, string subjectId)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = subjectId,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test",
		};

		_ = await harness.Service.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(false);

		return request.RequestId;
	}
}
