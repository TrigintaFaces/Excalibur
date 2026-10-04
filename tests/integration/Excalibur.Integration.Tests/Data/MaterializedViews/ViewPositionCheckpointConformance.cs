// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Integration.Tests.Data.MaterializedViews;

/// <summary>
/// The view-position checkpoint contract, written once and run against every provider that implements it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arms live here rather than per provider because the two defects they bind were ASYMMETRY
/// defects.</b> Both reached shipped code, and both were missed the same way: an arm was run against one
/// provider and not against another, and the one that went unrun was the one that failed. Writing the arms
/// against <see cref="IMaterializedViewStore"/> makes that particular mistake inexpressible — a provider
/// either runs the whole contract or does not appear.
/// </para>
/// <para>
/// <b>What the two defects were</b>, because an arm whose reason is forgotten gets weakened later:
/// </para>
/// <list type="number">
/// <item>
/// A rebuild cleared checkpoints by ADVANCING to zero. Every provider refuses that, because the advance is
/// deliberately monotonic — and the method returned <c>void</c>, so the refusal could not be observed and
/// each store logged a successful save. The one test that covered it passed because its in-memory double
/// assigned positions unconditionally, which no real store does.
/// </item>
/// <item>
/// The first repair used the position as a document's external version on the search stores. A delete does
/// not forget a version — it increments it and retains a tombstone — so a low advance stayed refused even
/// after the document was gone. The reset worked and the next advance did not.
/// </item>
/// </list>
/// <para>
/// <see cref="AcceptALowAdvanceImmediatelyAfterAReset"/> is RED against either defect, and could not have
/// been written against a mock: the behaviour under test belongs to the engine.
/// </para>
/// </remarks>
public abstract class ViewPositionCheckpointConformance
{
	/// <summary>The view name every arm operates on.</summary>
	protected const string ViewName = "OrderSummary";

	/// <summary>
	/// Builds a store against real infrastructure, with no checkpoint recorded for <see cref="ViewName"/>.
	/// </summary>
	/// <remarks>
	/// An implementation MUST clear prior state through its own fixture rather than by calling
	/// <see cref="IMaterializedViewStore.ResetPositionAsync"/>, or a defect in the code under test
	/// quietly becomes this suite's setup.
	/// </remarks>
	protected abstract Task<IMaterializedViewStore> NewStoreAsync();

	[Fact]
	public async Task AdvanceTheCheckpointAndReportThatItMoved()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);

		var first = await store.SavePositionAsync(ViewName, 100, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		var second = await store.SavePositionAsync(ViewName, 200, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		first.ShouldBe(ViewPositionSaveOutcome.Advanced);
		second.ShouldBe(ViewPositionSaveOutcome.Advanced);
		(await store.GetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false))
			.ShouldBe(200);
	}

	[Fact]
	public async Task RefuseAStaleAdvanceAndSayThatItRefused()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);
		_ = await store.SavePositionAsync(ViewName, 200, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var lower = await store.SavePositionAsync(ViewName, 150, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		lower.ShouldBe(
			ViewPositionSaveOutcome.RefusedAsStale,
			"a delayed or retried write carrying an older position must not rewind the checkpoint, and the "
			+ "caller must be able to tell that it did not");
		(await store.GetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false))
			.ShouldBe(200, "a refused write must leave the stored checkpoint untouched");
	}

	[Fact]
	public async Task RefuseAnAdvanceToTheSamePosition()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);
		_ = await store.SavePositionAsync(ViewName, 200, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var same = await store.SavePositionAsync(ViewName, 200, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		same.ShouldBe(
			ViewPositionSaveOutcome.RefusedAsStale,
			"re-writing the position already stored moves nothing, so it is a refusal rather than an "
			+ "advance -- the boundary case between the two outcomes");
	}

	[Fact]
	public async Task ClearTheCheckpointOnReset()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);
		_ = await store.SavePositionAsync(ViewName, 4_000_000, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		await store.ResetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false);

		(await store.GetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false))
			.ShouldBeNull(
				"a reset must leave NO checkpoint, so a replay starts from the beginning; a stored zero "
				+ "would be indistinguishable from a view legitimately checkpointed at zero");
	}

	/// <summary>
	/// THE ARM. A reset is only a reset if the next advance is accepted.
	/// </summary>
	/// <remarks>
	/// Resets from a HIGH position to a LOW one — four million down to one — deliberately. That gap is what
	/// made the external-version repair fail on the search stores, because the retained tombstone version
	/// exceeded every plausible position. An arm that reset from 2 to 1 would have passed against the
	/// defect, which is why the magnitude is part of the test rather than an arbitrary number.
	/// </remarks>
	[Fact]
	public async Task AcceptALowAdvanceImmediatelyAfterAReset()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);
		_ = await store.SavePositionAsync(ViewName, 4_000_000, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		await store.ResetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false);

		var afterReset = await store.SavePositionAsync(ViewName, 1, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		afterReset.ShouldBe(
			ViewPositionSaveOutcome.Advanced,
			"a rebuild resets the checkpoint and then checkpoints forward from the start; if the advance "
			+ "after a reset is refused, the reset bought nothing");
		(await store.GetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false))
			.ShouldBe(1);
	}

	[Fact]
	public async Task TreatAResetOfAnAbsentCheckpointAsSuccess()
	{
		var store = await NewStoreAsync().ConfigureAwait(false);

		await store.ResetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false);
		await store.ResetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false);

		(await store.GetPositionAsync(ViewName, TestContext.Current.CancellationToken).ConfigureAwait(false))
			.ShouldBeNull("clearing an already-absent checkpoint is success, not a refusal");
	}
}
