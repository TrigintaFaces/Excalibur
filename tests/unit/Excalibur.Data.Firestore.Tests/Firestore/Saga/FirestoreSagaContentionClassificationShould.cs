// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Firestore;

using Grpc.Core;

namespace Excalibur.Data.Tests.Firestore.Saga;

/// <summary>
/// Locks which gRPC status means "a concurrent writer won the save race" and therefore becomes a
/// <c>ConcurrencyException</c>, rather than crossing the abstraction as a transport exception.
/// </summary>
/// <remarks>
/// <para>
/// The arm that matters is <see cref="NotTreatAnUnavailableBackendAsContention"/>. The provider's retry
/// policy classifies UNAVAILABLE, DEADLINE_EXCEEDED, RESOURCE_EXHAUSTED and INTERNAL as transient
/// alongside ABORTED, so the tempting implementation reuses that set. Only ABORTED means a version race
/// was lost; the others mean no race took place, and reporting one as a concurrency conflict sends the
/// caller into a reload-and-retry loop against a backend that is simply down.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class FirestoreSagaContentionClassificationShould : UnitTestBase
{
	private static RpcException WithStatus(StatusCode code, string detail = "test") =>
		new(new Status(code, detail));

	[Fact]
	public void TreatAbortedAsContention() =>
		FirestoreSagaStore.IsTransactionContention(WithStatus(StatusCode.Aborted, "Transaction lock timeout."))
			.ShouldBeTrue(
				"Firestore reports transaction contention as ABORTED once RunTransactionAsync has spent its "
				+ "own retry budget, and the emulator carries 'Transaction lock timeout.' under that status. "
				+ "The write applied nothing, so the losing writer is rejected with a concurrency conflict.");

	[Fact]
	public void NotTreatAnUnavailableBackendAsContention() =>
		FirestoreSagaStore.IsTransactionContention(WithStatus(StatusCode.Unavailable))
			.ShouldBeFalse(
				"the retry policy calls UNAVAILABLE transient, but no version race took place -- the server "
				+ "was unreachable. Reporting it as a conflict would tell the caller to reload and try "
				+ "again, which cannot help while data access is broken.");

	[Theory]
	[InlineData(StatusCode.DeadlineExceeded)]
	[InlineData(StatusCode.ResourceExhausted)]
	[InlineData(StatusCode.Internal)]
	[InlineData(StatusCode.FailedPrecondition)]
	[InlineData(StatusCode.NotFound)]
	[InlineData(StatusCode.PermissionDenied)]
	[InlineData(StatusCode.Cancelled)]
	public void LetEveryOtherStatusSurface(StatusCode code) =>
		FirestoreSagaStore.IsTransactionContention(WithStatus(code))
			.ShouldBeFalse(
				"only ABORTED carries the lost-race meaning. FAILED_PRECONDITION is the notable one: the "
				+ "save path passes no Precondition, so it can only arise from a missing index or an "
				+ "unusable database -- broken data access wearing a retryable-looking status.");

	[Fact]
	public void RefuseToClassifyNothing() =>
		Should.Throw<ArgumentNullException>(() => FirestoreSagaStore.IsTransactionContention(null!));
}
