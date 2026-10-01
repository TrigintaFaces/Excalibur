// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The store half of the retry guarantee: a destroyed key recorded against a request is readable back from
/// that request, appends rather than replaces, and is idempotent.
/// </summary>
/// <remarks>
/// <para>
/// The service-level arms establish that a retry attests a handle its own earlier pass destroyed. They read
/// that handle from <see cref="ErasureStatus.DestroyedKeyHandles"/>, so they prove nothing about whether
/// anything ever WROTE it. These arms are the other half: without them the two sides could each pass over a
/// record that does not survive.
/// </para>
/// <para>
/// Append-only and idempotent are both load-bearing rather than tidiness. A pass that TRUNCATED the set would
/// erase the evidence of every earlier pass — the precise failure the record exists to prevent, arriving by a
/// different route — and a second record of the same handle happens on any retry that re-destroys a key the
/// store had already forgotten.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ADestroyedKeyRecordSurvivesForTheRetryShould
{
	/// <summary>
	/// SAFETY. A recorded handle is readable back from the request that recorded it.
	/// </summary>
	[Fact]
	public async Task ReturnAHandleItRecorded()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		await store.RecordKeyDestroyedAsync(requestId, "subject-key", CancellationToken.None).ConfigureAwait(true);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status.ShouldNotBeNull();
		status.DestroyedKeyHandles.ShouldContain("subject-key");
	}

	/// <summary>
	/// SAFETY. Recording a second handle keeps the first: the set is append-only across passes.
	/// </summary>
	[Fact]
	public async Task KeepEveryHandleAcrossSeveralRecords()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		await store.RecordKeyDestroyedAsync(requestId, "key-a", CancellationToken.None).ConfigureAwait(true);
		await store.RecordKeyDestroyedAsync(requestId, "key-b", CancellationToken.None).ConfigureAwait(true);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status.ShouldNotBeNull();
		status.DestroyedKeyHandles.OrderBy(static h => h, StringComparer.Ordinal)
			.ShouldBe(["key-a", "key-b"]);
	}

	/// <summary>
	/// SAFETY. Recording the same handle twice leaves one entry and raises nothing.
	/// </summary>
	[Fact]
	public async Task BeIdempotentForAHandleAlreadyRecorded()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		await store.RecordKeyDestroyedAsync(requestId, "subject-key", CancellationToken.None).ConfigureAwait(true);
		await store.RecordKeyDestroyedAsync(requestId, "subject-key", CancellationToken.None).ConfigureAwait(true);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status.ShouldNotBeNull();
		status.DestroyedKeyHandles.ShouldHaveSingleItem();
	}

	/// <summary>
	/// SAFETY. Handles are compared ordinally, so two handles differing only in case stay two handles.
	/// </summary>
	/// <remarks>
	/// A key handle is an opaque identifier, not text to be folded. Comparing case-insensitively would treat
	/// two distinct subjects' handles as one and attest coverage for a key that was never destroyed.
	/// </remarks>
	[Fact]
	public async Task TreatHandlesDifferingOnlyInCaseAsDistinct()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		await store.RecordKeyDestroyedAsync(requestId, "Key-A", CancellationToken.None).ConfigureAwait(true);
		await store.RecordKeyDestroyedAsync(requestId, "key-a", CancellationToken.None).ConfigureAwait(true);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status.ShouldNotBeNull();
		status.DestroyedKeyHandles.Count.ShouldBe(2);
	}

	/// <summary>
	/// LIVENESS. A request that has destroyed nothing reports an empty set, never null.
	/// </summary>
	/// <remarks>
	/// Without this, a store returning a null collection would pass every arm above and then fail the caller
	/// that enumerates it on an ordinary first pass.
	/// </remarks>
	[Fact]
	public async Task ReportNoHandlesForARequestThatHasDestroyedNothing()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(true);

		status.ShouldNotBeNull();
		status.DestroyedKeyHandles.ShouldNotBeNull();
		status.DestroyedKeyHandles.ShouldBeEmpty();
	}

	/// <summary>
	/// SAFETY. Recording against a request that does not exist throws rather than succeeding quietly.
	/// </summary>
	/// <remarks>
	/// The caller attests a destruction on the strength of this record existing. A silent no-op here would
	/// hand it a false assurance and leave the retry unable to attest the destruction either.
	/// </remarks>
	[Fact]
	public async Task RefuseToRecordAgainstARequestThatDoesNotExist()
	{
		var store = CreateStore();

		_ = await Should.ThrowAsync<KeyNotFoundException>(
			() => store.RecordKeyDestroyedAsync(Guid.NewGuid(), "subject-key", CancellationToken.None))
			.ConfigureAwait(true);
	}

	/// <summary>
	/// SAFETY. An empty handle is refused, because it names no key.
	/// </summary>
	[Fact]
	public async Task RefuseAnEmptyHandle()
	{
		var store = CreateStore();
		var requestId = await SubmitAsync(store).ConfigureAwait(true);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.RecordKeyDestroyedAsync(requestId, string.Empty, CancellationToken.None))
			.ConfigureAwait(true);
	}

	private static InMemoryErasureStore CreateStore() =>
		new(
			TestDataSubjectHasher.Instance,
			UntenantedContext.Instance,
			Options.Create(new TenantContextOptions { RequireTenant = false }));

	private static async Task<Guid> SubmitAsync(InMemoryErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "a-subject",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(true);

		return request.RequestId;
	}
}
