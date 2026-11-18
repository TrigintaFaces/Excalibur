// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// A host that stops mid-erasure must still RECORD what happened to the request.
/// </summary>
/// <remarks>
/// <para>
/// The execution handler records its terminal status through the erasure store. When that write carried the
/// CALLER'S cancellation token, a graceful shutdown made it unlandable: the token is already cancelled by the
/// time the handler runs, and both SQL stores open their connection with <c>OpenAsync(cancellationToken)</c>,
/// so the write threw instead of recording. The request then stayed in its claimed status forever -- with keys
/// possibly already destroyed, no certificate, no error and no metric -- and nothing in the system enumerates
/// a claimed request again: the scheduled-execution query matches only <c>Scheduled</c>, the key-destruction
/// query matches only <c>AwaitingKeyDestruction</c>, the scheduler deliberately exempts the claimed status from
/// reset, and the execution entry point refuses it.
/// </para>
/// <para>
/// <b>Catastrophic by silence rather than by severity.</b> A crash is loud and recoverable; a partially
/// performed irreversible erasure that reports nothing is not.
/// </para>
/// <para>
/// <b>This arm bounds what the fix claims, and the bound matters.</b> Recording the outcome makes the stopped
/// erasure VISIBLE and terminal. It does not make it RE-EXECUTABLE: a terminal status is not re-enumerated by
/// any discovery query either, so a request interrupted mid-destruction still needs an operator or a leased
/// claim to carry it forward. The arm therefore asserts a recorded outcome, not a recovered erasure.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class AnInterruptedErasureRecordsItsOutcomeShould
{
	[Fact]
	public async Task RecordATerminalStatusWhenTheHostCancelsMidExecution()
	{
		using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		using var hostStopping = new CancellationTokenSource();

		// Models the host's shutdown token tripping at an arbitrary point inside the execution, and a real SQL
		// store's behaviour once it has: OpenAsync(cancellationToken) throws rather than writing.
		var tripped = false;
		var harness = new ErasureLifecycleHarness(keys, keys, decorateStore: store =>
		{
			var wrapped = A.Fake<IErasureStore>(options => options.Wrapping(store));

			A.CallTo(() => wrapped.UpdateStatusAsync(A<Guid>._, A<ErasureRequestStatus>._, A<ErasureRequestStatus?>._, A<string?>._, A<string?>._, A<CancellationToken>._))
				.ReturnsLazily((Guid id, ErasureRequestStatus status, ErasureRequestStatus? expected, string? error, string? claimIdentity, CancellationToken ct) =>
				{
					if (ct.IsCancellationRequested)
					{
						// What a real store does with a cancelled token: it does not write.
						return Task.FromException<ErasureWriteOutcome>(new OperationCanceledException(ct));

					}

					// The erasure's own outcome write is the moment the host begins stopping.
					if (!tripped
						&& status is ErasureRequestStatus.Failed or ErasureRequestStatus.PartiallyCompleted)
					{
						tripped = true;
						hostStopping.Cancel();
						return Task.FromException<ErasureWriteOutcome>(new OperationCanceledException(hostStopping.Token));
					}

					return store.UpdateStatusAsync(id, status, expected, error, claimIdentity, ct);
				});

			A.CallTo(() => wrapped.RecordCompletionAsync(
					A<Guid>._, A<int>._, A<int>._, A<Guid>._, A<string>._, A<CancellationToken>._))
				.ReturnsLazily((Guid id, int keyCount, int records, Guid certificateId, string claimIdentity, CancellationToken ct) =>
				{
					if (!tripped)
					{
						tripped = true;
						hostStopping.Cancel();
						return Task.FromException<ErasureWriteOutcome>(new OperationCanceledException(hostStopping.Token));
					}

					return store.RecordCompletionAsync(id, keyCount, records, certificateId, claimIdentity, ct);
				});

			return wrapped;
		});

		var request = new ErasureRequest
		{
			DataSubjectId = "interrupted-by-shutdown",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test",
		};

		_ = await harness.Service.RequestErasureAsync(request, CancellationToken.None).ConfigureAwait(true);
		var requestId = request.RequestId;

		// The execution must not propagate the cancellation in place of recording an outcome. Before the fix
		// the terminal write reused this very token, so it threw from inside the handler and escaped here.
		ErasureExecutionResult? result = null;
		Exception? escaped = null;

		try
		{
			result = await harness.Service.ExecuteAsync(requestId, hostStopping.Token).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			escaped = ex;
		}

		escaped.ShouldBeNull(
			"the execution propagated the host's cancellation instead of recording the request's outcome, so "
			+ "the request is left in its claimed status and no pass will enumerate it again");

		result!.Success.ShouldBeFalse("the erasure was interrupted, so it did not succeed");
		tripped.ShouldBeTrue("the arm never reached the interleaving it exists to test");

		// THE PROPERTY: the request is not left in its claimed status with nothing recorded.
		var status = await harness.StatusOfAsync(requestId).ConfigureAwait(true);
		status.ShouldNotBe(ErasureRequestStatus.InProgress,
			"a request claimed by a process that stopped must not stay claimed -- nothing ever enumerates it again");
		status.ShouldBe(ErasureRequestStatus.Failed);
	}
}
