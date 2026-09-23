// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The erasure lifecycle on a key store that can only SCHEDULE key destruction: the request must reach a state that
/// is terminal or revisitable -- never neither -- and must never reach <see cref="ErasureRequestStatus.Completed"/>, or
/// receive a certificate, while its key still exists.
/// </summary>
/// <remarks>
/// SAFETY: no Completed and no certificate while any key of the request is recoverable.
/// LIVENESS: a request whose keys the provider has destroyed reaches Completed once the completion pass runs; and a
/// request that genuinely failed still reaches a failure state (so "mark everything terminal" cannot pass).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureAwaitingKeyDestructionShould
{
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly IKeyManagementProvider _keyProvider = KeyDestructionFakes.ProviderThatReportsDestruction();

	[Fact]
	public async Task Enter_awaiting_key_destruction_when_the_only_outstanding_work_is_a_scheduled_key()
	{
		// The provider scheduled the key rather than destroying it. That is the ONLY blocker, so this is a correct
		// intermediate state -- not PartiallyCompleted, which nothing revisits and which reads as a failure.
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);

		var (requestId, _, result) = await harness.SubmitAndExecuteAsync("subject-a");

		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
		result.Success.ShouldBeFalse("a scheduled key is not an erased key");
		result.KeysDeleted.ShouldBe(0);
	}

	[Fact]
	public async Task Not_issue_a_certificate_for_a_request_awaiting_key_destruction()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-b");

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => harness.Service.GenerateCertificateAsync(requestId, CancellationToken.None));
		(await harness.Store.GetCertificateAsync(requestId, CancellationToken.None)).ShouldBeNull(
			"no completion certificate may exist while the key is still recoverable");
	}

	[Fact]
	public async Task Leave_the_request_revisitable_while_the_provider_still_holds_the_key()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("subject-c");
		StubProviderStillHolds(keyId);

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
		(await harness.Store.GetCertificateAsync(requestId, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task Not_treat_the_reported_destruction_time_as_proof_of_destruction()
	{
		// The provider reported a destruction instant that is ALREADY IN THE PAST, yet still holds the key (it was
		// recovered, or its purge lags). A stored time is never proof: only the provider's answer completes.
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(-1));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("subject-d");
		StubProviderStillHolds(keyId);

		_ = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
	}

	[Fact]
	public async Task Not_complete_or_certify_when_the_provider_cannot_confirm_destruction_even_if_the_key_is_not_found()
	{
		// THE RULED ARM. This provider does not implement IKeyDestructionStatusProvider, and its lookup answers
		// "not found" -- which is also what a backend with a recovery window says about a key it can still
		// restore. "Not found" is therefore never read as "destroyed": the request stays awaiting destruction and
		// no certificate is issued. The liveness partner is the arm below, on a provider that can answer.
		var cannotConfirm = A.Fake<IKeyManagementProvider>();
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, cannotConfirm);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("subject-m");
		A.CallTo(() => cannotConfirm.GetKeyAsync(keyId, A<CancellationToken>._)).Returns(Task.FromResult<KeyMetadata?>(null));

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
		(await harness.Store.GetCertificateAsync(requestId, CancellationToken.None)).ShouldBeNull(
			"a key the provider cannot confirm destroyed may still be recoverable");
		harness.Store.CertificateCount.ShouldBe(0);
	}

	[Fact]
	public async Task Complete_and_certify_once_the_provider_confirms_destruction()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("subject-e");
		StubProviderStillHolds(keyId);
		_ = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		// The window elapses; the provider reports the key destroyed.
		_keyProvider.ReportsDestroyed(keyId, true);

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(1);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
		var certificate = await harness.Service.GenerateCertificateAsync(requestId, CancellationToken.None);
		certificate.Payload.Verification.Verified.ShouldBeTrue();
		certificate.Payload.Verification.DeletedKeyIds.ShouldContain(keyId);
		certificate.Payload.Verification.Warnings.ShouldNotBeEmpty(
			"a certificate issued at confirmation discloses that it followed a provider's destruction window");
	}

	[Fact]
	public async Task Keep_a_genuinely_failed_erasure_on_the_failure_path()
	{
		// LIVENESS of the failure half: a contributor genuinely failed. Even though the key is also merely
		// scheduled, this is NOT a correct intermediate state and must not be parked as one.
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var failing = A.Fake<IErasureContributor>();
		A.CallTo(() => failing.Name).Returns("event-store");
		A.CallTo(() => failing.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(ErasureContributorResult.Failed("store unreachable")));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider, [failing]);

		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-f");

		var status = await harness.StatusOfAsync(requestId);
		status.ShouldBeOneOf(ErasureRequestStatus.Failed, ErasureRequestStatus.PartiallyCompleted);
		status.ShouldNotBe(ErasureRequestStatus.AwaitingKeyDestruction);
	}

	[Fact]
	public async Task Reach_a_failure_state_when_key_destruction_itself_fails()
	{
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("provider unreachable"));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);

		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-g");

		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Failed);
	}

	[Fact]
	public async Task Not_re_execute_a_request_awaiting_key_destruction()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-h");

		var again = await harness.Service.ExecuteAsync(requestId, CancellationToken.None);

		again.Success.ShouldBeFalse();
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Not_allow_cancelling_a_request_whose_key_is_already_being_destroyed()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-i");

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => harness.Service.CancelErasureAsync(requestId, "changed mind", "operator", CancellationToken.None));
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
	}

	[Fact]
	public async Task Leave_requests_waiting_rather_than_complete_them_unconfirmed_when_no_verifier_is_registered()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider, withVerifier: false);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("subject-j");
		_keyProvider.ReportsDestroyed(keyId, true);

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
	}

	[Fact]
	public async Task Complete_immediately_on_a_key_store_that_destroys_at_once()
	{
		// The in-memory provider destroys immediately when asked for zero retention: terminal on execution, and the
		// completion pass has nothing to do.
		using var inMemory = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var harness = new ErasureLifecycleHarness(inMemory, inMemory);
		var subjectKey = TestDataSubjectHasher.Instance.HashDataSubjectId("subject-k");
		_ = await inMemory.RotateKeyAsync(subjectKey, EncryptionAlgorithm.Aes256Gcm, "crypto-shred", null, CancellationToken.None);

		var (requestId, _, result) = await harness.SubmitAndExecuteAsync("subject-k");

		result.Success.ShouldBeTrue();
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
		(await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None)).ShouldBe(0);
	}

	[Fact]
	public async Task Not_report_awaiting_requests_as_due_for_execution()
	{
		StubDeleteScheduled(DateTimeOffset.UtcNow.AddDays(7));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-l");

		var due = await harness.Store.GetScheduledRequestsAsync(100, CancellationToken.None);

		due.ShouldNotContain(r => r.RequestId == requestId);
	}

	private void StubDeleteScheduled(DateTimeOffset irreversibleAt) =>
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(irreversibleAt)));

	private void StubProviderStillHolds(string keyId) => _keyProvider.ReportsDestroyed(keyId, false);
}
