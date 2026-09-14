// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Mongo lock on WHICH refusal a superseded leader receives from the fenced mark-sent path: a tenure
/// that has been overtaken must be told its token is STALE, never that the message was ALREADY SENT.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the distinction is a safety property and not a nicety.</b> The two refusals route differently in
/// the caller. <see cref="StaleOutboxFencingTokenException"/> means "you are not the leader any more, stop"
/// — the message stays where the winning tenure left it. A generic failure is treated as a delivery fault,
/// and the superseded leader then marks the message failed (its lease fields are null after the winner's
/// mark-sent, so the ownership guard admits it) or dead-letters it. The consequence is a message that WAS
/// delivered being recorded as failed or dead-lettered by the loser of a leadership race.
/// </para>
/// <para>
/// <b>Two guards stand between a stale token and this outcome, and they are not equivalent.</b> The
/// SCOPE-WIDE control-doc CAS fails closed on any token below the recorded high-water. The PER-DOCUMENT
/// predicate is the narrower backstop for the round-trip gap between that check and the mutation. This arm
/// deliberately exercises the ordinary path — claim under the winning token, mark sent, then present the
/// superseded token — because that is the interleaving a real deployment produces.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> make the refusal order test already-sent before it tests the fence and this arm
/// observes the generic failure instead of the stale-token refusal → RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxSupersededMarkSentDiagnosisShould : IClassFixture<MongoDbOutboxStoreContainerFixture>
{
	private const long SupersededToken = 1;
	private const long WinningToken = 2;

	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	public MongoDbOutboxSupersededMarkSentDiagnosisShould(MongoDbOutboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	private MongoDbOutboxStore NewStore()
	{
		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});
		return new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance);
	}

	[Fact]
	public async Task TellASupersededLeaderItsTokenIsStale_NotThatTheMessageWasAlreadySent()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the refusal a superseded leader receives decides whether a DELIVERED message is recorded as "
			+ "failed or dead-lettered — this real-Mongo lock must never be skipped");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		// The winning tenure claims under its token and completes the delivery.
		var winner = NewStore();
		await winner.StageMessageAsync(
			new OutboundMessage("test.message", [1], "dest"),
			CancellationToken.None).ConfigureAwait(false);

		var claimed = (await winner.GetUnsentMessagesAsync(10, WinningToken, CancellationToken.None)
			.ConfigureAwait(false)).ToList();
		claimed.Count.ShouldBe(1, "the winning tenure must claim the staged message before it can mark it sent");
		var messageId = claimed[0].Id;

		await winner.MarkSentAsync(messageId, WinningToken, CancellationToken.None).ConfigureAwait(false);

		// The superseded tenure — still running, unaware it lost — presents its older token for the same
		// message. A separate store instance, because the two tenures are separate processes.
		var superseded = NewStore();
		var refusal = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await superseded.MarkSentAsync(messageId, SupersededToken, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		refusal.PresentedToken.ShouldBe(SupersededToken);
		refusal.HighWaterToken.ShouldBe(
			WinningToken,
			"the refusal must name the token that superseded this one, so an operator can tell a lost "
			+ "leadership race from a delivery fault");
	}

	/// <summary>
	/// The same refusal, in the one state where the PER-DOCUMENT predicate is the only guard standing:
	/// the scope-wide control doc is absent, so it admits the stale token and the message's own recorded
	/// token is all that remains to refuse it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is not a contrived state.</b> The control doc and the messages live in two different
	/// collections, so any operation that restores, migrates, or clears one without the other leaves
	/// exactly this asymmetry — the messages still carry the tokens of the tenure that claimed them while
	/// the high-water mark has been forgotten. A forgotten high-water is admitted rather than refused,
	/// because an absent mark cannot be above anything, which is correct on its own terms and is precisely
	/// what hands the decision to the per-document predicate.
	/// </para>
	/// <para>
	/// The arm above cannot reach that predicate: with the control doc intact, the scope-wide CAS refuses
	/// the stale token first and the per-document check is never consulted. So the ordering of the two
	/// refusals below it is only ever observable HERE, which is what makes this arm the one that binds it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task StillRefuseAsStale_WhenTheScopeWideHighWaterHasBeenLostAndOnlyTheDocumentRemembers()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"this is the only interleaving in which the per-document fencing predicate is reachable — "
			+ "skipping it leaves that predicate unexercised by any real-infra arm");
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var winner = NewStore();
		await winner.StageMessageAsync(
			new OutboundMessage("test.message", [1], "dest"),
			CancellationToken.None).ConfigureAwait(false);

		var claimed = (await winner.GetUnsentMessagesAsync(10, WinningToken, CancellationToken.None)
			.ConfigureAwait(false)).ToList();
		claimed.Count.ShouldBe(1, "the winning tenure must claim the staged message before it can mark it sent");
		var messageId = claimed[0].Id;

		await winner.MarkSentAsync(messageId, WinningToken, CancellationToken.None).ConfigureAwait(false);

		// Lose the scope-wide high-water while the message keeps the winning tenure's token.
		var options = new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		};
		var database = new MongoDB.Driver.MongoClient(options.ConnectionString)
			.GetDatabase(options.DatabaseName);
		await database.DropCollectionAsync(options.CollectionName + "__fence", CancellationToken.None)
			.ConfigureAwait(false);

		// The superseded tenure now presents its stale token against a scope that no longer remembers it
		// was superseded. Only the token recorded on the message itself can still refuse this.
		var superseded = NewStore();
		var refusal = await Should.ThrowAsync<StaleOutboxFencingTokenException>(
			async () => await superseded.MarkSentAsync(messageId, SupersededToken, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		refusal.PresentedToken.ShouldBe(SupersededToken);
		refusal.HighWaterToken.ShouldBe(
			WinningToken,
			"the message's own token is the surviving evidence of the lost race, so it must be the token "
			+ "the refusal reports");
	}
}
