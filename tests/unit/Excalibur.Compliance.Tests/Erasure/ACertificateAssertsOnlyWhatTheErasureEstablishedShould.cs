// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The signed erasure certificate asserts what the erasure established and nothing else — and it is issued
/// either way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two properties, and they pull in opposite directions, which is why they are bound together here.</b>
/// A claim nobody established must not be attested; and the document must always be produced. Signing
/// happens AFTER the irreversible act — keys are destroyed, then contributors run, then the certificate is
/// signed — so refusing to issue one would leave a consumer with the destruction performed and no evidence
/// that it was performed. An arm that only checked the first property would be satisfied by a signer that
/// refuses everything, which is why every safety arm below is paired with a liveness arm.
/// </para>
/// <para>
/// <b>The measurements run against the real crypto-shredding stack</b> — the real key admin, the real
/// in-memory erasure store, a real retention registry and an ordinary contributor. Nothing here needs
/// reflection, a deserializer, or anything a consumer mapping their own configuration would not write.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ACertificateAssertsOnlyWhatTheErasureEstablishedShould
{
	private const string Subject = "the-subject-whose-certificate-must-be-honest";
	/// <summary>
	/// An aggregate type nothing has written to before the erasure runs, so its per-class handle is absent
	/// from the mint ledger at the instant the erasure enumerates.
	/// </summary>
	private const string ClassWrittenDuringErasure = "AClassFirstWrittenMidErasure";

	private const string RetainedType = "SalesRecord";
	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.";

	/// <summary>
	/// SAFETY. An out-of-range Article 17(3) ground is not attested, and the erasure is recorded as NOT
	/// complete rather than silently exempt.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An exemption whose basis was never established IS an unmet obligation, and presenting an unmet
	/// obligation as a lawful ground is what turns a failure into a defensible retention. So it folds into
	/// the erasure's errors, which is the machinery that already makes an attestation of completion
	/// unreachable — not a new refusal path.
	/// </para>
	/// <para>
	/// RED input: delete the basis validation from the signing boundary. Today a basis of 99 reaches
	/// <c>Payload.Exceptions[0].Basis</c> as 99 and the signature verifies, so this arm fails on both
	/// counts — the value is attested and the erasure reports complete.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_attest_a_legal_basis_that_names_no_article_17_3_ground()
	{
		await using var stack = new HonestCertificateStack(contributorBasis: (LegalHoldBasis)99);

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"an exemption whose ground was never established is an unmet obligation, and an erasure with an "
			+ "unmet obligation has not completed");

		var entry = outcome.Certificate.Payload.Exceptions.ShouldHaveSingleItem(
			"the arm is vacuous unless the record actually carries the retention to inspect");
		entry.Basis.ShouldBe(
			LegalHoldBasis.NotEstablished,
			"99 names no Article 17(3) ground, so the certificate must say the ground was not established "
			+ "rather than present 99 to an auditor as one");
	}

	/// <summary>
	/// SAFETY, and it is the SILENT half — the one an <c>Enum.IsDefined</c> check cannot reach. A contributor
	/// that never states a basis does not thereby claim freedom of expression.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm is stated separately from the one above rather than folded into it, because the two fail to
	/// different implementations. <c>Enum.IsDefined</c> catches 99 and passes 0 — and 0 is
	/// <see cref="LegalHoldBasis.FreedomOfExpression"/>, Article 17(3)(a). So a validation written as
	/// "IsDefined" leaves a contributor mapping an unset configuration value asserting freedom of expression
	/// over a tax retention, on a signed document, and verifying.
	/// </para>
	/// <para>
	/// RED input: make <c>ErasureException.Basis</c> non-nullable again, or validate it with
	/// <c>Enum.IsDefined</c> alone. Either way <c>default</c> becomes a defined member and this arm fails
	/// while the arm above still passes — which is the whole reason it exists.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_read_an_unstated_basis_as_freedom_of_expression()
	{
		// `default` is the case this arm is about: a contributor that never assigned a basis. It now lands on
		// NotEstablished rather than on FreedomOfExpression, which is the whole point of the renumber -- and
		// the reason this arm is stated separately from the out-of-range one.
		await using var stack = new HonestCertificateStack(contributorBasis: default);

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"nobody stated a ground for this retention, so the obligation is unmet and the erasure did not "
			+ "complete");

		var entry = outcome.Certificate.Payload.Exceptions.ShouldHaveSingleItem();
		entry.Basis.ShouldNotBe(
			LegalHoldBasis.FreedomOfExpression,
			"an auditor reading this certificate would see Article 17(3)(a) as the ground a tax record was "
			+ "kept under, which nobody claimed");
		entry.Basis.ShouldBe(
			LegalHoldBasis.NotEstablished,
			"and the honest value is the enum's zero, which is what an unassigned value produces");
	}

	/// <summary>
	/// LIVENESS, and it is the non-vacuity control for both arms above: a lawful retention with a real ground
	/// is attested exactly as before, and the erasure completes.
	/// </summary>
	/// <remarks>
	/// If this arm reddens, the validation is refusing lawful retentions and the fix is worse than the defect
	/// it was written for. It is also what stops the two arms above being satisfied by a signer that strips
	/// every basis.
	/// </remarks>
	[Fact]
	public async Task Attest_a_lawful_retention_whose_ground_was_established()
	{
		await using var stack = new HonestCertificateStack();

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldBe(
			ErasureRequestStatus.Completed,
			$"every claim on this certificate holds, so nothing may stand between this erasure and "
			+ $"completion. What the erasure reported instead: {outcome.Detail}");

		var entry = outcome.Certificate.Payload.Exceptions.ShouldHaveSingleItem();
		entry.Basis.ShouldBe(
			LegalHoldBasis.LegalObligation,
			"the ground was stated and is one this framework defines, so it is attested unchanged");
		entry.DataCategory.ShouldBe(RetainedType);
	}

	/// <summary>
	/// SAFETY, and it is owed whatever the claims say: the certificate is ISSUED even when a claim did not
	/// hold. Destruction already happened, so the evidence is owed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that forbids the obvious implementation. Refusing at the signing boundary would close
	/// every arm above and leave the consumer strictly worse off than the defect did: keys destroyed, request
	/// terminal, and no signed record that any of it happened. What the certificate must do is record that
	/// the erasure did not complete — not vanish.
	/// </para>
	/// <para>
	/// RED input: make the unestablished-claim path throw, or skip the certificate write. Both leave a
	/// consumer with an irreversible act performed and nothing to show an auditor.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Issue_the_certificate_even_when_a_claim_did_not_hold()
	{
		await using var stack = new HonestCertificateStack(contributorBasis: (LegalHoldBasis)99);

		var outcome = await stack.EraseAsync();

		outcome.KeysDestroyed.ShouldBeGreaterThan(
			0,
			"the arm is worthless unless the irreversible act genuinely happened; with nothing destroyed "
			+ "there is no evidence owed and a missing certificate costs the consumer nothing");

		outcome.Certificate.Signature.ShouldNotBeNullOrWhiteSpace(
			"the destruction is done and cannot be redone, so the signed record of it is owed regardless of "
			+ "what the claims turned out to say");

		stack.VerifyAsIssued(outcome.Certificate).ShouldBe(
			ErasureCertificateVerificationResult.Verified,
			"an issued certificate nobody can authenticate is not evidence");
	}

	/// <summary>
	/// SAFETY. No signed certificate carries a retention length with no instant to measure it from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A bare duration reads as computable. "Six years" leaves the reader to supply the missing instant, and
	/// the only instant in front of them is the certificate's own date — which restarts a statutory clock at
	/// the moment the data subject asked to be erased and extends the retention past the end the law gives
	/// it. The certificate would be evidence for a longer retention than the obligation supports, in a
	/// document produced to prove the opposite.
	/// </para>
	/// <para>
	/// The fixture's contributor DOES emit a six-year period, exactly as a declaration-driven contributor
	/// does, so this arm has a real value to strip rather than an absence to agree with.
	/// </para>
	/// <para>
	/// RED input: remove the strip at the signing boundary. The contributor's duration then reaches the
	/// signed payload and the assertion fails. A second RED input: restore the contributor's
	/// <c>RetentionPeriod</c> copy AND remove the strip — the arm names both because either alone is a
	/// producer that emits one.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_state_a_retention_length_with_no_instant_to_measure_it_from()
	{
		await using var stack = new HonestCertificateStack(contributorPeriod: TimeSpan.FromDays(365 * 6));

		var outcome = await stack.EraseAsync();

		var entry = outcome.Certificate.Payload.Exceptions.ShouldHaveSingleItem(
			"the arm is vacuous unless a retention is on the record to inspect");

		entry.RetentionPeriod.ShouldBeNull(
			"a reader cannot obtain an end from a duration this document does not anchor, so the honest form "
			+ "of the claim is its absence. The entry still states the ground, the category, the reason and "
			+ "the handle whose destruction ends the retention");
	}

	/// <summary>
	/// SAFETY, and it is the reason this seam's blast radius is catastrophic: an erasure does not attest
	/// completeness over a window it did not close.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Key destruction runs once, before the contributors, and establishes the state of those handles at THAT
	/// instant and nothing later. A write for the same data subject landing afterwards finds no key at the
	/// handle and mints a live one — so at the moment the certificate is signed there is personal data of an
	/// erased subject under a live key, and a certificate signed on the earlier reading asserts a fact about
	/// now from a measurement taken then. Nothing downstream ever learns otherwise, which is what makes it
	/// the silent class.
	/// </para>
	/// <para>
	/// The write here is performed BY a contributor, through the real field encryptor, which is precisely
	/// where a concurrent request's write lands in the ordering: after destruction, before signing.
	/// </para>
	/// <para>
	/// RED input: remove the re-establish step before the outcome is decided. The certificate then attests
	/// completion over the field written during the erasure.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_attest_completion_over_data_written_while_the_erasure_ran()
	{
		await using var stack = new HonestCertificateStack(writeDuringErasure: true);

		var outcome = await stack.EraseAsync();

		outcome.KeysDestroyed.ShouldBeGreaterThan(
			0,
			"the arm is vacuous unless a handle was genuinely destroyed; with nothing destroyed there is no "
			+ "handle whose state could have changed and the re-establish step has no subject");

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"a field for this data subject was written after the key was destroyed and encrypted under a "
			+ "live key, so the erasure did not cover it and must not attest that it did");
	}

	/// <summary>
	/// SAFETY, second half: the outcome DISTINGUISHES "completed" from "completed except for data written
	/// during the erasure". Silence is not an available answer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A type that cannot express the second forces the first, which is how this reached a signed document.
	/// With only <c>Completed</c> and <c>PartiallyCompleted</c> available, an erasure overtaken by a write had
	/// to be reported as one of them: <c>Completed</c> attests an erasure that did not happen, and
	/// <c>PartiallyCompleted</c> says something failed when nothing did. A reader could tell neither apart
	/// from the genuine article.
	/// </para>
	/// <para>
	/// RED input: delete the third state, and the arm above can only report <c>PartiallyCompleted</c> — which
	/// this arm rejects by name, because it tells an auditor a failure occurred where none did.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Distinguish_a_concurrent_write_from_a_partial_failure()
	{
		await using var stack = new HonestCertificateStack(writeDuringErasure: true);

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldBe(
			ErasureRequestStatus.CompletedExceptConcurrentWrites,
			"nothing the erasure attempted failed, and it did not complete either. Reporting this as a "
			+ "partial failure tells an auditor something broke when nothing did, and reporting it as "
			+ "completed attests an erasure over data written after the key was destroyed");

		outcome.Certificate.Signature.ShouldNotBeNullOrWhiteSpace(
			"the destruction happened, so the evidence is owed here too");
	}

	/// <summary>
	/// LIVENESS for the re-establish step, and it is what stops every arm above being satisfied by an
	/// erasure that can never complete.
	/// </summary>
	/// <remarks>
	/// No write lands during this erasure, so every destroyed handle is still destroyed when the certificate
	/// is signed, and the erasure completes. If this reddens, the re-establish step is reporting a re-mint
	/// that did not happen and no erasure can ever be certified.
	/// </remarks>
	[Fact]
	public async Task Complete_when_no_write_landed_during_the_erasure()
	{
		await using var stack = new HonestCertificateStack();

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldBe(
			ErasureRequestStatus.Completed,
			$"every handle this erasure destroyed was still destroyed when it finished, so there is nothing "
			+ $"standing between it and completion. What the erasure reported instead: {outcome.Detail}");
	}

	/// <summary>
	/// SAFETY, and it is the end-to-end half of the concurrent-write property: a mint-ledger row whose key
	/// creation was never confirmed stops this erasure from attesting a completion.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the one state no amount of re-reading can detect without the row saying so.</b> The row is
	/// written BEFORE the key is created, so while a write sits between those two steps the ledger's handle
	/// set is unchanged since the erasure read it and the key material reads absent because it genuinely is
	/// not there yet. That is observationally identical to a writer that crashed between its row and its
	/// mint -- which is safe and correctly ignored -- and the two diverge only after the certificate is
	/// signed. The pending row is the only observable that separates them.
	/// </para>
	/// <para>
	/// The arm records an intent and never confirms it, which is exactly the state a write in flight leaves
	/// behind, then runs a real erasure and binds that the outcome is NOT Completed.
	/// </para>
	/// <para>
	/// RED input: have the erasure read only the minted set, or drop the error it raises per pending handle.
	/// Either way the pending row is detected and then certified over anyway, which is the original defect
	/// -- the detection has to cost an error or it changes nothing.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_attest_completion_while_a_mint_is_still_pending()
	{
		await using var stack = new HonestCertificateStack();

		stack.LeaveAMintPendingDuringTheErasure();

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"a mint-ledger row whose key creation was never confirmed is a write in flight, and its key may "
			+ "come into existence after this erasure finishes. Attesting a completed erasure over it would "
			+ "be a signed certificate covering personal data that is about to become readable, which "
			+ "nothing downstream could ever detect. What the erasure reported instead: "
			+ outcome.Detail);

		outcome.Status.ShouldBe(
			ErasureRequestStatus.CompletedExceptConcurrentWrites,
			"nothing the erasure ATTEMPTED failed -- the keys it set out to destroy are gone and a write "
			+ "arrived alongside -- so the outcome that says exactly that is the honest one. Reporting a "
			+ "partial failure would tell an auditor something broke when nothing did. What the erasure "
			+ "reported instead: " + outcome.Detail);
	}

	/// <summary>
	/// SAFETY, and the one an adversarial review of the fix above constructed: a handle the erasure
	/// ENUMERATED, attempted, and found nothing at, whose key is then created before the attestation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the case that reports through no other path, and it is reachable with one ordinary
	/// writer.</b> A ledger row is written before its key, so a row can exist while its key does not. The
	/// erasure enumerates that row -- pending rows are part of the set it must decide about -- attempts the
	/// destruction, and the key store answers that nothing is there. That is an idempotent no-op by design
	/// and raises no error, because a handle whose key never existed has no ciphertext this erasure could
	/// have made unreadable. Then the write finishes: it creates the key and confirms the row it had
	/// already written.
	/// </para>
	/// <para>
	/// <b>Nothing else looks at it.</b> It is not in the destroyed set, so the re-occupation probe cannot
	/// reach it. It is no longer pending, so the two-state row cannot. And it IS in the enumerated set,
	/// which is what an earlier version of this fix subtracted from the probe's domain -- so the handle was
	/// enumerated, acted on, and examined by nothing, and the certificate said Completed.
	/// </para>
	/// <para>
	/// ENUMERATED IS AN INTENT; DESTROYED IS A FACT. This arm is the set where they differ.
	/// </para>
	/// <para>
	/// RED input: subtract the enumerated set from the attestation's probe domain, which is what the
	/// previous revision did and what the sibling arm below cannot detect.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when the outcome has been asserted.</returns>
	[Fact]
	public async Task Not_attest_completion_when_a_handle_it_found_empty_is_minted_before_the_attestation()
	{
		await using var stack = new HonestCertificateStack();

		stack.CompleteAPendingMintDuringTheErasure();

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"the erasure enumerated this handle, tried to destroy it, found nothing there -- which is "
			+ "correctly not an error -- and the write then created the key. Live material for an erased "
			+ "subject sits at a handle the certificate claims to have covered, and no probe over the "
			+ "DESTROYED set can see it, because this erasure destroyed nothing at it. What the erasure "
			+ "reported instead: " + outcome.Detail);

		outcome.Status.ShouldBe(
			ErasureRequestStatus.CompletedExceptConcurrentWrites,
			"nothing the erasure ATTEMPTED failed, so the outcome naming a write alongside is the honest "
			+ "one. What the erasure reported instead: " + outcome.Detail);
	}

	/// <summary>
	/// SAFETY, and the gap a formal model found rather than a test: a write that COMPLETES between the
	/// enumeration and the attestation, at a handle this erasure never enumerated, stops it attesting a
	/// completion.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This handle is in none of the three sets the erasure used to examine.</b> It is not in the
	/// enumerated set, because that set was read before the write existed. It is not in the destroyed set,
	/// because the erasure never set out to destroy a handle it had not heard of. And it is not PENDING,
	/// because the write finished -- so the two-state mint row, which catches a write still in flight, sees
	/// nothing either. Each of those three mechanisms is correct and none of them reaches this.
	/// </para>
	/// <para>
	/// <b>It needs no unusual behaviour to reach.</b> The write here goes into an aggregate type nothing had
	/// written to before, which derives a per-class handle absent from the ledger when the erasure
	/// enumerated. A subject who is re-registered mid-erasure, or who keeps trading because the law requires
	/// their identity kept, does exactly this. The old outcome was a signed certificate asserting an erasure
	/// over personal data that is readable right now, with nothing raised anywhere.
	/// </para>
	/// <para>
	/// RED input: narrow the attestation probe's domain back to the handles this pass destroyed. The write
	/// is then invisible to every check and the erasure reports Completed, which is the defect.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when the outcome has been asserted.</returns>
	[Fact]
	public async Task Not_attest_completion_when_a_write_lands_at_a_handle_it_never_enumerated()
	{
		await using var stack = new HonestCertificateStack(writeANewClassDuringErasure: true);

		var outcome = await stack.EraseAsync();

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"a handle holding live material for this subject, which this erasure neither destroyed nor "
			+ "retained deliberately, is personal data the certificate would be attesting the erasure of. "
			+ "The handle was minted after the enumeration, so it is in neither the enumerated set nor the "
			+ "destroyed set, and its mint completed, so it is not pending either -- which is why a probe "
			+ "over the LEDGER is the only thing that can see it. What the erasure reported instead: "
			+ outcome.Detail);

		outcome.Status.ShouldBe(
			ErasureRequestStatus.CompletedExceptConcurrentWrites,
			"nothing the erasure ATTEMPTED failed, so the outcome that says a write arrived alongside is "
			+ "the honest one. Reporting a partial failure would tell an auditor something broke when "
			+ "nothing did. What the erasure reported instead: " + outcome.Detail);
	}

	/// <summary>
	/// SAFETY. A provider that cannot say whether a key is destroyed leaves the key state unmeasured, and an
	/// unmeasured state is recorded as such rather than assumed clean.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the branch the re-establish step takes when there is nothing to ask. Silence is the one answer
	/// it must not give: an erasure that destroyed keys and cannot establish they stayed destroyed has not
	/// measured the thing its certificate would attest. Every key provider this framework ships implements the
	/// capability, and startup validation already warns a deployment whose provider does not — so this arm
	/// binds the outcome for the deployment that ignored that warning.
	/// </para>
	/// <para>
	/// RED input: skip the check when the capability is absent. The erasure then completes on a measurement
	/// nobody took, which is the same defect as the concurrent-write case arriving through a different door.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_attest_completion_when_the_provider_cannot_report_key_destruction()
	{
		await using var stack = new HonestCertificateStack(keyAdminCanReportDestruction: false);

		var outcome = await stack.EraseAsync();

		outcome.KeysDestroyed.ShouldBeGreaterThan(
			0,
			"the arm is vacuous unless keys were genuinely destroyed; an erasure that destroyed nothing has no "
			+ "key state to re-establish and correctly produces no finding");

		outcome.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			"the framework cannot establish that the keys it destroyed stayed destroyed, so it must not attest "
			+ "a completion that rests on the measurement it could not take");

		outcome.Certificate.Signature.ShouldNotBeNullOrWhiteSpace(
			"and the evidence is still owed: the keys are gone either way");
	}

	/// <summary>
	/// What an erasure produced: the outcome the store recorded, and the certificate a consumer would read.
	/// </summary>
	private sealed record ErasureOutcome(
		ErasureRequestStatus Status,
		ErasureCertificate Certificate,
		int KeysDestroyed,
		string? Detail);

	/// <summary>
	/// The real crypto-shredding stack, a real key admin and a real erasure store, with one contributor whose
	/// claims each arm varies.
	/// </summary>
	private sealed class HonestCertificateStack : IAsyncDisposable
	{
		private readonly ServiceProvider _provider;
		private readonly ILegalHoldService _legalHolds = A.Fake<ILegalHoldService>();
		private readonly LegalHoldBasis _contributorBasis;
		private readonly TimeSpan? _contributorPeriod;
		private readonly bool _writeDuringErasure;
		private bool _writeANewClassDuringErasure;
		private readonly bool _keyAdminCanReportDestruction;
		private readonly byte[] _signingKey = new byte[32];
		private bool _started;

		// contributorBasis is the ground the contributor claims. The sentinel is "not supplied", which yields
		// the ordinary lawful case; an explicit null is the never-stated case, and an out-of-range cast is the
		// loud one. contributorPeriod is the duration a declaration-driven contributor copies onto its entry.
		// writeDuringErasure makes the contributor encrypt a personal field for the SAME subject while the
		// erasure is running -- which is where a concurrent request's write lands in the ordering.
		public HonestCertificateStack(
			LegalHoldBasis contributorBasis = LegalHoldBasis.LegalObligation,
			TimeSpan? contributorPeriod = null,
			bool writeDuringErasure = false,
			bool keyAdminCanReportDestruction = true,
			bool writeANewClassDuringErasure = false)
		{
			_writeANewClassDuringErasure = writeANewClassDuringErasure;
			_contributorBasis = contributorBasis;
			_contributorPeriod = contributorPeriod;
			_writeDuringErasure = writeDuringErasure;
			_keyAdminCanReportDestruction = keyAdminCanReportDestruction;

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddDataSubjectHashing();
			_ = services.Configure<DataSubjectHashingOptions>(options =>
				options.Pepper = "test-pepper-0123456789abcdef0123456789ab");
			_ = services.AddEncryption(builder =>
				builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));

			// The REAL provider as the key admin, so the destruction is real AND the capability that answers
			// "is this handle destroyed?" is the provider's own rather than a fake's configured answer. A fake
			// admin would leave the re-establish step unable to ask, which is a different arm.
			_ = services.AddSingleton<IKeyManagementAdmin>(sp =>
				(IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

			_ = services.AddErasureRetention(new ErasureRetention
			{
				AggregateType = RetainedType,
				Tenant = KeyedTenantPartition.Untenanted,
				Basis = LegalHoldBasis.LegalObligation,
				Justification = Justification,
				RetentionPeriod = TimeSpan.FromDays(365 * 6),
			});

			_ = services.AddCryptoShredding();
			_ = services.AddDefaultTenantContext();
						// The store reads the instant a claim lapse is decided against from a clock no caller can
			// supply, so a container that registers it by type owes that clock. The production
			// registration supplies it; this fixture builds its own container and must too.
			_ = services.AddSingleton(TimeProvider.System);
			_ = services.AddSingleton<InMemoryErasureStore>();
			_ = services.AddSingleton<IErasureStore>(sp => sp.GetRequiredService<InMemoryErasureStore>());

			// The ledger facet of the same store. The read path takes it as a required collaborator and a
			// tombstone is produced only from a record in it, so without this mapping the stack cannot even be
			// constructed -- and with it, a destruction the erasure service performs is recorded where a later
			// read can find it.
			_ = services.AddSingleton<IKeyDestructionLedger>(sp => sp.GetRequiredService<InMemoryErasureStore>());

			// The write path records a mint intent for every first write per subject and obligation class,
			// so the crypto-shredding stack cannot encrypt anything without one. Resolved from the same
			// store as the destruction ledger, which is what the production registration does.
			_ = services.AddSingleton<IKeyMintLedger>(sp => sp.GetRequiredService<InMemoryErasureStore>());

			// This fixture wires a VOLATILE mint ledger by hand rather than through
			// AddInMemoryErasureStore(), so nothing in its registration announces that the rows are lost on
			// restart -- and startup correctly refuses that. These arms are about which handles an erasure
			// destroys within a single process, so the statement is true here and stated rather than
			// side-stepped by relaxing the guard.
			_ = services.AcknowledgeNonDurableErasureLedger();

			_provider = services.BuildServiceProvider();
		}

		public async ValueTask<ErasureOutcome> EraseAsync()
		{
			await StartAsync();

			// The subject holds data OUTSIDE the retained type, which is what gives the erasure a key to
			// destroy at all: without it the subject's own handle was never minted, nothing is destroyed, and
			// several arms below would be vacuous.
			var encryptor = _provider.GetRequiredService<IFieldEncryptor>();
			_ = await encryptor.EncryptAsync(
				Subject,
				RetentionScope.NotInAnAggregate,
				"the customer's own record"u8.ToArray(),
				TestContext.Current.CancellationToken);

			var requestId = await GivenAnErasureAsync();

			if (_completePendingMintDuringErasure)
			{
				var stamped = await Store.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
				await RecordAnUnconfirmedMintAsync(stamped!.TenantId, ClassWrittenDuringErasure);
			}

			if (_pendingMintDuringErasure)
			{
				// AFTER the request exists, so the owner is the one the store stamped, and BEFORE the service
				// runs, so the row is already on file when the erasure enumerates -- the dominant shape of a
				// write in flight when an erasure begins.
				var stamped = await Store.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
				await RecordAnUnconfirmedMintAsync(stamped!.TenantId);
			}

			var result = await NewService().ExecuteAsync(requestId, TestContext.Current.CancellationToken);

			var status = await Store.GetStatusAsync(requestId, TestContext.Current.CancellationToken);
			status.ShouldNotBeNull("every arm here reads the outcome the store recorded; it must have recorded");

			var certificate = await Store.GetCertificateAsync(requestId, TestContext.Current.CancellationToken);
			certificate.ShouldNotBeNull(
				"the certificate is ALWAYS owed: the destruction is irreversible, so a consumer left without "
				+ "one has an act performed and no evidence of it");

			return new ErasureOutcome(status!.Status, certificate!, result.KeysDeleted, status.ErrorMessage);
		}

		/// <summary>Verifies the certificate exactly as a consumer holding the document would.</summary>
		public ErasureCertificateVerificationResult VerifyAsIssued(ErasureCertificate certificate) =>
			ErasureCertificateVerifier.Verify(certificate, _signingKey);

		public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

		private bool _pendingMintDuringErasure;
		private bool _completePendingMintDuringErasure;

		private InMemoryErasureStore Store => _provider.GetRequiredService<InMemoryErasureStore>();

		/// <summary>
		/// Arranges for an unconfirmed mint-ledger row to exist when the erasure enumerates.
		/// </summary>
		internal void LeaveAMintPendingDuringTheErasure() => _pendingMintDuringErasure = true;

		/// <summary>
		/// Arranges the one state no probe over the destroyed set can reach: a ledger row that exists when
		/// the erasure enumerates, whose key does NOT exist when the erasure tries to destroy it, and whose
		/// key is created before the attestation.
		/// </summary>
		/// <remarks>
		/// The row is recorded at the handle the mid-erasure write will DERIVE, so that write completes this
		/// same row instead of creating a second one -- which is exactly what the production write path does
		/// when it finds a row of its own already present. The erasure therefore enumerates the handle,
		/// attempts to destroy it, finds nothing there, and reports nothing: the absent branch is an
		/// idempotent no-op by design. Only a probe whose domain is the LEDGER sees it afterwards.
		/// </remarks>
		internal void CompleteAPendingMintDuringTheErasure()
		{
			_completePendingMintDuringErasure = true;
			_writeANewClassDuringErasure = true;
		}

		/// <summary>
		/// Records a mint-ledger row for this stack's data subject and deliberately never confirms it,
		/// which is the state a write in flight leaves behind between its row and its key.
		/// </summary>
		/// <returns>A task that completes when the unconfirmed row is on file.</returns>
		/// <remarks>
		/// The handle and the subject token are derived through the SAME functions the write path uses, so
		/// the row this records is the row a real concurrent write would have left -- not a value this test
		/// invented, which would bind nothing about the erasure's own enumeration.
		/// </remarks>
		internal async Task RecordAnUnconfirmedMintAsync(
			string? recordedTenant,
			string obligationClass = "AWriteInFlight")
		{
			var hasher = _provider.GetRequiredService<IDataSubjectHasher>();
			// THE OWNER THE REQUEST RECORDED, read back from the store rather than assumed. The service
			// derives its enumeration owner from status.TenantId by this same function, so taking any
			// other value here would record a row the erasure never looks for -- the arm would pass
			// while binding nothing.
			var owner = SubjectKeyHandle.OwningTenant(recordedTenant);
			var handle = SubjectKeyHandle.ForSubject(owner, Subject, hasher).Retained(obligationClass);

			await ((IKeyMintLedger)Store).RecordMintIntentAsync(
				handle.Value,
				owner,
				hasher.HashDataSubjectId(Subject),
				TestContext.Current.CancellationToken);
		}

		private async ValueTask StartAsync()
		{
			if (_started)
			{
				return;
			}

			_started = true;
			_ = _provider.GetServices<IEncryptionProvider>().ToList();
			foreach (var hostedService in _provider.GetServices<IHostedService>())
			{
				await hostedService.StartAsync(TestContext.Current.CancellationToken);
			}
		}

		private async ValueTask<Guid> GivenAnErasureAsync()
		{
			var requestId = Guid.NewGuid();

			await Store.SaveRequestAsync(
				new ErasureRequest
				{
					RequestId = requestId,
					DataSubjectId = Subject,
					IdType = DataSubjectIdType.UserId,
					Scope = ErasureScope.User,
					LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
					RequestedBy = "admin",
					RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
				},
				DateTimeOffset.UtcNow,
				TestContext.Current.CancellationToken);

			A.CallTo(() => _legalHolds.CheckHoldsAsync(
					A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
				.Returns(Task.FromResult(
					new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

			return requestId;
		}

		/// <summary>
		/// The key admin the erasure destroys through: the real provider, or — for the arm about a provider that
		/// cannot answer — one that destroys keys and reports nothing about them afterwards.
		/// </summary>
		/// <remarks>
		/// The substitute still reports a real destruction, so the erasure has keys to have destroyed; what it
		/// lacks is the capability to say they stayed destroyed. That is the whole difference under test, and
		/// pairing it with a genuine destruction is what keeps the arm from passing for the wrong reason.
		/// </remarks>
		private IKeyManagementAdmin KeyAdmin()
		{
			if (_keyAdminCanReportDestruction)
			{
				return _provider.GetRequiredService<IKeyManagementAdmin>();
			}

			var real = _provider.GetRequiredService<IKeyManagementAdmin>();

			// Blind to the DESTRUCTION-STATUS capability and nothing else. It still forwards the key lookup to
			// the real provider, because erasure reads a key's generation before destroying it and records the
			// destruction against that generation -- a substitute that could not answer the lookup would destroy
			// nothing it could record, and this arm's own non-vacuity guard (keys were genuinely destroyed) would
			// fail for a reason that has nothing to do with the capability under test.
			var blind = A.Fake<IKeyManagementAdmin>(o => o.Implements<IKeyManagementProvider>());
			A.CallTo(() => blind.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
				.ReturnsLazily((string keyId, int days, CancellationToken ct) => real.DeleteKeyAsync(keyId, days, ct));
			A.CallTo(() => ((IKeyManagementProvider)blind).GetKeyAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string keyId, CancellationToken ct) =>
					((IKeyManagementProvider)real).GetKeyAsync(keyId, ct));
			return blind;
		}

		private ErasureService NewService() =>
			new(Store,
				KeyAdmin(),
				Microsoft.Extensions.Options.Options.Create(new ErasureOptions
				{
					KeyShredOnlyErasure = true,
					Retention = new ErasureRetentionOptions { SigningKey = _signingKey },
				}),
				NullLogger<ErasureService>.Instance,
				_provider.GetRequiredService<IDataSubjectHasher>(),
				_legalHolds,
				null,
				null,

				// The domain this fixture stands up annotates nothing, and the annotated-coverage gate is a
				// SEPARATE guarantee with its own arms. Left on the reflection default it scans this whole test
				// assembly and reports every [PersonalData] category any other fixture declares as personal data
				// the inventory never located -- which blocks completion for a reason that has nothing to do with
				// the claims these arms are about, and would make every liveness arm here unreachable.
				TestAnnotationSource.None,
				_provider.GetRequiredService<IErasureRetentionRegistry>(),

				// THE SAME INSTANCE as the destruction ledger and the erasure store, deliberately: the
				// production registration resolves all three from one store, so a separate mint ledger
				// here would let these arms pass against a composition the framework cannot produce.
				Store,
				[
					new RetainedContributor(
						_contributorBasis,
						_contributorPeriod,
						_writeDuringErasure || _writeANewClassDuringErasure
							? _provider.GetRequiredService<IFieldEncryptor>()
							: null,
						// OUTSIDE any aggregate, so the write lands on the PLAIN handle this erasure
						// destroyed and the re-occupation probe is what catches it -- or INTO a class
						// whose handle was never minted, so the handle is in neither the enumerated set
						// nor the destroyed set and only a probe over the LEDGER can catch it. Two
						// different defects, one parameter, and the arms name which they are about.
						_writeANewClassDuringErasure
							? RetentionScope.For(ClassWrittenDuringErasure)
							: RetentionScope.NotInAnAggregate),
				]);

		/// <summary>
		/// The shape a contributor has when one aggregate type is lawfully retained — and, when asked, the
		/// shape a request concurrent with the erasure has: it writes a personal field for the same subject
		/// while the erasure is between destroying keys and signing its certificate.
		/// </summary>
		private sealed class RetainedContributor(
			LegalHoldBasis basis,
			TimeSpan? period,
			IFieldEncryptor? writesDuringErasure,
			RetentionScope writeScope) : IErasureContributor
		{
			public string Name => "EventStore";

			public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

			public async Task<ErasureContributorResult> EraseAsync(
				ErasureContributorContext context,
				CancellationToken cancellationToken)
			{
				if (writesDuringErasure is not null)
				{
					// An ordinary write, through the real encryptor, for the subject being erased. It finds no
					// key at the destroyed handle and mints a live one -- which is the whole defect, and it
					// needs no unusual behaviour to reach: a re-registration, or a subject still trading
					// because the law requires their identity kept, does exactly this.
					_ = await writesDuringErasure.EncryptAsync(
						Subject,
						writeScope,
						"a field written while the erasure was running"u8.ToArray(),
						cancellationToken);
				}

				return ErasureContributorResult.Succeeded(
					1,
					[],
					[
						new ErasureException
						{
							Basis = basis,
							DataCategory = RetainedType,
							Reason = Justification,
							RetentionPeriod = period,
						},
					]);
			}
		}
	}
}
