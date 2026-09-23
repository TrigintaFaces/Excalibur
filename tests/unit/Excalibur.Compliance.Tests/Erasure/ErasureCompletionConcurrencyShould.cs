// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Interleavings of the erasure completion step: two confirmers must produce ONE certificate, and the scheduler
/// must never hand back for execution a request another execution owns or has finished.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureCompletionConcurrencyShould
{
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly IKeyManagementProvider _keyProvider = KeyDestructionFakes.ProviderThatReportsDestruction();

	[Fact]
	public void Derive_the_same_confirmation_certificate_id_for_the_same_request()
	{
		var requestId = Guid.NewGuid();

		ScheduledKeyDestructions.ConfirmationCertificateId(requestId)
			.ShouldBe(ScheduledKeyDestructions.ConfirmationCertificateId(requestId));
		ScheduledKeyDestructions.ConfirmationCertificateId(requestId)
			.ShouldNotBe(ScheduledKeyDestructions.ConfirmationCertificateId(Guid.NewGuid()));
	}

	[Fact]
	public async Task Adopt_the_certificate_a_racing_confirmer_already_issued_instead_of_issuing_a_second()
	{
		// The winner of the race inserted its certificate and has not yet recorded completion (or crashed there).
		// The loser must not add a second signed certificate for the same request; it records completion
		// against the one that exists.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(KeyDestructionOutcome.ScheduledAt(DateTimeOffset.UtcNow.AddDays(7))));
		var harness = new ErasureLifecycleHarness(_keyAdmin, _keyProvider);
		var (requestId, keyId, _) = await harness.SubmitAndExecuteAsync("race-subject");
		_keyProvider.ReportsDestroyed(keyId, true);

		var winner = await IssueWinnersCertificateAsync(harness, requestId);

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(1);
		var status = await harness.Store.GetStatusAsync(requestId, CancellationToken.None);
		status!.Status.ShouldBe(ErasureRequestStatus.Completed);
		status.CertificateId.ShouldBe(winner);
		harness.Store.CertificateCount.ShouldBe(1, "one request, one certificate");
	}

	[Theory]
	[InlineData(ErasureRequestStatus.InProgress)]
	[InlineData(ErasureRequestStatus.AwaitingKeyDestruction)]
	[InlineData(ErasureRequestStatus.Completed)]
	[InlineData(ErasureRequestStatus.Cancelled)]
	public async Task Not_reschedule_a_request_another_execution_owns_or_has_finished(ErasureRequestStatus current)
	{
		// Two scheduler instances listed the same request; the other one claimed it. This instance's execution is
		// refused ("concurrent execution detected"), which the scheduler sees as a failure. Resetting the request
		// to Scheduled would hand it back for a second execution, or make it cancellable beside a key already
		// being destroyed.
		var requestId = Guid.NewGuid();
		var store = A.Fake<IErasureStore>();
		var executor = A.Fake<IErasureExecutor>();
		var query = A.Fake<IErasureQueryStore>();
		var secondCycle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var polls = 0;

		A.CallTo(() => store.GetService(typeof(IErasureQueryStore))).Returns(query);
		A.CallTo(() => store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(StatusOf(requestId, current)));
		A.CallTo(() => query.GetScheduledRequestsAsync(A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (Interlocked.Increment(ref polls) == 2)
				{
					_ = secondCycle.TrySetResult();
				}

				return Task.FromResult<IReadOnlyList<ErasureStatus>>(
					polls == 1 ? [StatusOf(requestId, ErasureRequestStatus.Scheduled)] : []);
			});
		A.CallTo(() => executor.ExecuteAsync(requestId, A<CancellationToken>._))
			.Returns(ErasureExecutionResult.Failed("Request is no longer in Scheduled status (concurrent execution detected)"));

		var provider = A.Fake<IServiceProvider>();
		A.CallTo(() => provider.GetService(typeof(IErasureStore))).Returns(store);
		A.CallTo(() => provider.GetService(typeof(IErasureExecutor))).Returns(executor);
		A.CallTo(() => provider.GetService(typeof(IErasureCompletionProcessor))).Returns(null);
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(provider);
		var scopeFactory = A.Fake<IServiceScopeFactory>();
		A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

		var sut = new ErasureSchedulerBackgroundService(
			scopeFactory,
			Microsoft.Extensions.Options.Options.Create(new ErasureSchedulerOptions
			{
				Enabled = true,
				PollingInterval = TimeSpan.FromMilliseconds(20),
				MaxRetryAttempts = 3,
			}),
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		await sut.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondCycle.Task, global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
		await cts.CancelAsync();
		await sut.StopAsync(CancellationToken.None);

		A.CallTo(() => store.UpdateStatusAsync(requestId, A<ErasureRequestStatus>._, A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	private static async Task<Guid> IssueWinnersCertificateAsync(ErasureLifecycleHarness harness, Guid requestId)
	{
		var id = ScheduledKeyDestructions.ConfirmationCertificateId(requestId);
		var payload = new ErasureCertificatePayload
		{
			CertificateId = id,
			RequestId = requestId,
			DataSubjectReference = "winner",
			RequestReceivedAt = DateTimeOffset.UtcNow,
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary { KeysDeleted = 1, RecordsAffected = 0, DataCategories = [], TablesAffected = [] },
			Verification = new VerificationSummary { Verified = true, Methods = VerificationMethod.KeyManagementSystem, VerifiedAt = DateTimeOffset.UtcNow },
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
		};
		await harness.Store.SaveCertificateAsync(
			new ErasureCertificate { Payload = payload, Signature = "winner" }, CancellationToken.None);
		return id;
	}

	private static ErasureStatus StatusOf(Guid requestId, ErasureRequestStatus status) => new()
	{
		RequestId = requestId,
		DataSubjectIdHash = "hash",
		IdType = DataSubjectIdType.Email,
		Scope = ErasureScope.User,
		LegalBasis = ErasureLegalBasis.DataSubjectRequest,
		Status = status,
		RequestedBy = "test",
		RequestedAt = DateTimeOffset.UtcNow.AddDays(-1),
		ScheduledExecutionAt = DateTimeOffset.UtcNow.AddMinutes(-5),
		UpdatedAt = DateTimeOffset.UtcNow,
	};
}
