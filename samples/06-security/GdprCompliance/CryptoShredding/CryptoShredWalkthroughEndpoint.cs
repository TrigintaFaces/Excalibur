// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Erasure;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Erasure;

using GdprCompliance.Domain;
using GdprCompliance.Retention;

namespace GdprCompliance.CryptoShredding;

/// <summary>
/// The crypto-shredding walkthrough endpoint, plus the two helpers the walkthroughs in
/// <c>Program.cs</c> share.
/// </summary>
internal static class CryptoShredWalkthroughEndpoint
{
	/// <summary>
	/// Maps <c>POST /crypto-shred/walkthrough</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The guarantee this demonstrates, in one sentence: a personal field reads back as
	/// <see langword="null"/> ONLY where a durable destruction ledger holds a row for the key generation
	/// that field's envelope names.
	/// </para>
	/// <para>
	/// Every step exists to make that falsifiable from the outside: the stored form is ciphertext and is
	/// printed, so "encrypted at rest" is visible rather than asserted; the same stored bytes decrypt to
	/// the plaintext BEFORE the erasure, so the null afterwards cannot be confused with a record that was
	/// never readable; the ledger is asked directly, before and after, so the reason for the null is on
	/// the response; and a key generation this deployment freshly minted and never destroyed is asked of
	/// the same ledger through the same call, so the affirmative answer is a lookup rather than a
	/// constant.
	/// </para>
	/// <para>
	/// Note what the read does NOT do. It asks no key backend whether the key is still there. Absence
	/// cannot serve as the predicate: a backend with a recovery window reports a soft-deleted key as
	/// absent for the whole window while one restore call brings it back, so a tombstone derived from
	/// absence would report an erasure that did not happen. A ledger row is written only after an
	/// irreversible destruction this deployment performed has already completed, so the row's existence
	/// IS the destruction statement.
	/// </para>
	/// </remarks>
	/// <param name="app">The route builder to map onto.</param>
	internal static void MapCryptoShredWalkthrough(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		_ = app.MapPost("/crypto-shred/walkthrough", async (
			SubjectFieldCryptor cryptor,
			ISubjectKeyManager keyManager,
			IKeyDestructionLedger ledger,
			IErasureService erasureService,
			IDataSubjectHasher hasher,
			SubjectAggregateIndex index,
			IEventSourcedRepository<CustomerProfile, Guid> profiles,
			CancellationToken ct) =>
		{
			var customerId = Guid.NewGuid();
			var dataSubjectId = customerId.ToString("D");

			// READ THE GENERATION BEFORE ANYTHING ELSE, and this ordering is a contract rather than a
			// tidiness choice. The generation identifier is backend material that the destruction takes
			// with it, and the next ordinary write mints a NEW generation at the same handle — so asking
			// afterwards would answer truthfully about material this walkthrough is not holding. The
			// framework's own erasure path reads it in the same order, before it destroys anything.
			//
			// THE TENANT MUST BE THE ONE THE WRITE USES, because a subject's key handle is scoped to it:
			// naming a different tenant here would resolve a different handle, and the generation read
			// from it would not be the one the stored envelope names. This deployment is single-tenant, so
			// that is the default tenant identity — the same one the field encryptor resolves from the
			// ambient tenant context when no multi-tenancy is configured. A MULTI-TENANT host must pass
			// the tenant actually in scope, not this constant.
			//
			// Only this walkthrough needs the argument at all. A consumer who encrypts and decrypts
			// through SubjectFieldCryptor or IFieldEncryptor never names a tenant: those resolve it from
			// the ambient context. It is needed here solely because printing the ledger's answer requires
			// naming the generation, and nothing on the public surface can read that back out of a stored
			// envelope.
			var subjectKey = await keyManager
				.GetOrCreateKeyAsync(
					new TenantId(TenantDefaults.DefaultTenantId),
					dataSubjectId,
					RetentionScope.NotInAnAggregate,
					ct)
				.ConfigureAwait(false);
			var generation = subjectKey.Generation?.ToString() ?? string.Empty;

			// --- 1. Write: encrypt the personal fields in place ---------------------------------
			var stored = new SupportNote
			{
				CustomerId = dataSubjectId,
				FullName = "Erin Vance",
				EmailAddress = "erin@example.com",
				Subject = "Replacement wing mirror",
			};

			// aggregateType: null — this note is not stored inside an aggregate, so its fields are
			// protected by the subject's OWN key handle, which is the one an erasure destroys
			// unconditionally. Naming an aggregate type here would only matter if that type were under a
			// declared erasure retention, in which case the fields would be protected by the retained
			// record's key and survive instead.
			await cryptor.EncryptFieldsAsync(stored, aggregateType: null, ct).ConfigureAwait(false);

			// `stored` now holds what the database would hold. Encryption mutates in place, so every read
			// below works from a COPY of it — the stored bytes are never re-encrypted or mutated again.
			var storedFullName = stored.FullName;

			// --- 2. Live read: the stored form decrypts to the plaintext ------------------------
			var beforeErasure = stored with { };
			await cryptor.DecryptFieldsAsync(beforeErasure, ct).ConfigureAwait(false);

			// The ledger is keyed on (handle, generation), not on the generation alone. The handle passed
			// here SHOULD be the one the stored envelope names, because that is what identifies the
			// material actually being read; this sample passes the handle the key manager resolved
			// instead, which agrees with it for anything this framework wrote. It is a re-derivation
			// rather than a read because nothing on the public surface can return an envelope's handle:
			// see the comment on the generation read above.
			var ledgerSaysDestroyedBefore = await ledger
				.IsGenerationDestroyedAsync(subjectKey.KeyId, generation, ct)
				.ConfigureAwait(false);

			// --- 3. Erase the subject -----------------------------------------------------------
			// First, give this subject an event-sourced profile and tell the mapping where it is.
			//
			// THIS IS NOT DECORATION, and the reason is the coverage model rather than realism. The data
			// locations this host registered at startup are on the event store, and an erasure cannot
			// reach Completed while a REGISTERED location goes undischarged — a contributor that reports
			// success without naming the table-and-field pairs it erased discharges nothing. A subject
			// with no event-store data would therefore have its key destroyed and its request still end
			// Failed, naming the two locations nobody erased.
			//
			// The note above is in a store this host never registered as a data location, so no
			// contributor visits it and the certificate makes no claim about it — yet the note's fields
			// become unreadable anyway, because the key they were encrypted under is gone. That is the
			// whole value of crypto-shredding, and also its boundary: it reaches ciphertext written under
			// the destroyed key wherever it lives, and it is not a substitute for a data inventory.
			await profiles
				.SaveAsync(CustomerProfile.Register(customerId, "Erin Vance", "erin@example.com"), ct)
				.ConfigureAwait(false);

			index.Add(
				hasher.HashDataSubjectId(dataSubjectId),
				new AggregateReference(dataSubjectId, nameof(CustomerProfile)));

			// The erasure service owns the destruction, not the key manager: it honours legal holds
			// first, reads and durably stages the generation, destroys the key, and only then records the
			// destruction — a row written before the destruction would report live material as erased for
			// as long as the gap lasted.
			var filed = await erasureService.RequestErasureAsync(
				new ErasureRequest
				{
					DataSubjectId = dataSubjectId,
					IdType = DataSubjectIdType.UserId,
					RequestedBy = "api/crypto-shred/walkthrough",
					LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
					Scope = ErasureScope.User,

					// Demo only, so the scheduler picks it up now instead of in 72 hours. A real request
					// leaves this unset and takes the configured grace period — the subject's window to
					// change their mind, which matters because the destruction cannot be undone.
					GracePeriodOverride = TimeSpan.Zero,
				},
				ct).ConfigureAwait(false);

			var status = await WaitForErasureAsync(erasureService, filed.RequestId, ct).ConfigureAwait(false);

			var ledgerSaysDestroyedAfter = await ledger
				.IsGenerationDestroyedAsync(subjectKey.KeyId, generation, ct)
				.ConfigureAwait(false);

			// The control, and it is sharper than it looks because it holds the HANDLE FIXED. The same
			// handle, a generation minted here and never destroyed, must answer false — so the `true`
			// above is not a ledger that answers `true` to everything, and it is not a row that covers
			// the handle wholesale. The generation decides, within a handle, which is exactly the
			// property the ledger's (handle, generation) key exists to express: a destroyed key and a
			// key provisioned again at the same handle afterwards are different material, and only one
			// of them may produce a tombstone.
			var ledgerSaysDestroyedForUnrelatedGeneration = await ledger
				.IsGenerationDestroyedAsync(subjectKey.KeyId, KeyGeneration.Mint().ToString(), ct)
				.ConfigureAwait(false);

			// --- 4. Erased read: the SAME stored form now decrypts to null ----------------------
			var afterErasure = stored with { };
			await cryptor.DecryptFieldsAsync(afterErasure, ct).ConfigureAwait(false);

			return Results.Ok(new
			{
				DataSubjectId = dataSubjectId,
				KeyHandle = subjectKey.KeyId,
				KeyGeneration = generation,

				StoredAtRest = new
				{
					// The marker is a public constant, and it is what lets a reader tell a stored
					// ciphertext from a value that was never encrypted. Bare Base64 cannot express that
					// difference.
					Marker = EncryptedFieldBinding.StringEnvelopePrefix,
					FullName = Preview(storedFullName),
					EmailAddress = Preview(stored.EmailAddress),
					Subject = stored.Subject,   // not annotated, so still plaintext
				},

				BeforeErasure = new
				{
					LedgerSaysGenerationDestroyed = ledgerSaysDestroyedBefore,
					beforeErasure.FullName,
					beforeErasure.EmailAddress,
				},

				Erasure = new
				{
					RequestId = filed.RequestId,
					Status = status?.Status.ToString(),
				},

				AfterErasure = new
				{
					LedgerSaysGenerationDestroyed = ledgerSaysDestroyedAfter,
					LedgerSaysUnrelatedGenerationDestroyed = ledgerSaysDestroyedForUnrelatedGeneration,

					// The whole point. Null because the ledger holds a row, and for no other reason: a
					// key that merely could not be found would make this read THROW instead.
					afterErasure.FullName,
					afterErasure.EmailAddress,

					// Unannotated fields are untouched, which is what keeps a record loadable after an
					// erasure.
					afterErasure.Subject,
				},
			});
		});
	}

	/// <summary>
	/// Waits for a filed erasure to reach a terminal state.
	/// </summary>
	/// <remarks>
	/// Polls the request's own status rather than sleeping for a fixed interval, so a walkthrough returns
	/// as soon as the erasure is done — the scheduler executes it on its next poll. Returns the last
	/// status read even if it never settled, so a stall is visible in the response rather than hidden
	/// behind a thrown timeout.
	/// </remarks>
	/// <param name="erasureService">The erasure service the request was filed with.</param>
	/// <param name="requestId">The filed request.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The last status read, or <see langword="null"/> if the request could not be read at all.</returns>
	internal static async Task<ErasureStatus?> WaitForErasureAsync(
		IErasureService erasureService,
		Guid requestId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(erasureService);

		ErasureStatus? status = null;
		for (var attempt = 0; attempt < 60; attempt++)
		{
			status = await erasureService.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
			if (status?.Status is ErasureRequestStatus.Completed
				or ErasureRequestStatus.PartiallyCompleted
				or ErasureRequestStatus.Failed)
			{
				return status;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return status;
	}

	/// <summary>
	/// Shortens a stored ciphertext so the walkthrough response stays readable, while still showing the
	/// real marker and the real Base64 rather than a placeholder.
	/// </summary>
	internal static string Preview(string? value) => value is null
		? "(null)"
		: value.Length <= 80 ? value : value[..80] + $"... ({value.Length} chars total)";
}
