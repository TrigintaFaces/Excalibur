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
/// The multi-tenant crypto-shredding walkthrough: three tenants holding a record for the SAME data-subject
/// identifier, and one tenant's erasure.
/// </summary>
internal static class TwoTenantCryptoShredEndpoint
{
	/// <summary>
	/// The tenants this walkthrough writes as, in order. The first is the partition this host operates as
	/// when no tenant scope is open, which this framework names rather than leaves undecided — reads and
	/// writes there are confined to it exactly as a named tenant's are.
	/// </summary>
	private static readonly (string Label, string? TenantId)[] Tenants =
	[
		("untenanted", null),
		("tenant-contoso", "tenant-contoso"),
		("tenant-fabrikam", "tenant-fabrikam"),
	];

	/// <summary>
	/// Maps <c>POST /crypto-shred/two-tenants</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The guarantee this demonstrates: <b>a tenant is part of a subject key's identity, not a filter applied
	/// around it</b> — so tenants whose <c>[DataSubjectId]</c> values collide get different keys, and erasing
	/// the data subject in one tenant leaves every other tenant's record readable.
	/// </para>
	/// <para>
	/// Data-subject identifiers are consumer-supplied from consumer entities — an email address, a customer
	/// number, an external account id — so they repeat across tenants in ordinary deployments. If a handle
	/// were constant in the tenant, those tenants would share ONE key, and any one of them erasing would
	/// destroy it for all: the others would read <see langword="null"/> over data nobody asked to erase,
	/// while this framework asserted a lawful erasure of it.
	/// </para>
	/// <para>
	/// Both halves are on the response, and both are needed. The other tenants' fields still decrypting is
	/// the safety half; the erasing tenant's own field reading <see langword="null"/>, with the ledger row
	/// that is the only thing entitling it to, is the liveness half — an erasure that had silently stopped
	/// working altogether would satisfy the safety half on its own.
	/// </para>
	/// <para>
	/// <b>Where each layer gets its tenant from, because the three answers are deliberately different.</b>
	/// The WRITE path reads the ambient tenant, so each record is written inside a
	/// <see cref="TenantContextHolder.BeginScope"/> for the tenant it belongs to. The ERASURE path reads the
	/// tenant recorded against its own request, because a background processor's ambient scope is not
	/// evidence of whose data a request was about — so an erasure must be FILED by the tenant that owns it,
	/// and a tenant named in the request body is a declaration rather than the control. The READ path reads
	/// neither: every field of the envelope, the tenant included, is rebuilt from the stored bytes, so a
	/// projection rebuild or a background replay with no tenant scope at all still resolves the key its
	/// writer used. Every read below is therefore taken outside any scope.
	/// </para>
	/// </remarks>
	/// <param name="app">The route builder to map onto.</param>
	internal static void MapTwoTenantCryptoShred(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		_ = app.MapPost("/crypto-shred/two-tenants", async (
			SubjectFieldCryptor cryptor,
			ISubjectKeyManager keyManager,
			IKeyDestructionLedger ledger,
			IErasureService erasureService,
			IDataSubjectHasher hasher,
			SubjectAggregateIndex index,
			IEventSourcedRepository<CustomerProfile, Guid> profiles,
			CancellationToken ct) =>
		{
			// ONE identifier, used by every tenant below. This is the collision: the value is the consumer's,
			// and nothing stops two tenants holding a record for the same one.
			var collidingId = Guid.NewGuid();
			var dataSubjectId = collidingId.ToString("D");

			// --- Each tenant writes its own record for that one identifier ----------------------
			var written = new List<WrittenNote>();
			foreach (var (label, tenantId) in Tenants)
			{
				var (handle, generation, note) = await WriteNoteAsync(
					tenantId, dataSubjectId, $"erin@{label}.example", cryptor, keyManager, ct)
					.ConfigureAwait(false);

				var before = await DecryptCopyAsync(note, cryptor, ct).ConfigureAwait(false);
				written.Add(new WrittenNote(label, tenantId, handle, generation, note, before.EmailAddress));
			}

			// The erasing tenant is the first one, and it also needs event-store data — for the same reason as
			// the single-tenant walkthrough: the data locations this host registered at startup are on the
			// event store, and an erasure cannot reach Completed while a registered location goes
			// undischarged. Written in the same scope as that tenant's note, so its personal fields are
			// encrypted under the same key.
			var erasing = written[0];
			using (TenantContextHolder.BeginScope(erasing.TenantId))
			{
				await profiles
					.SaveAsync(CustomerProfile.Register(collidingId, "Erin Vance", erasing.EmailAddressBefore!), ct)
					.ConfigureAwait(false);
			}

			index.Add(
				hasher.HashDataSubjectId(dataSubjectId),
				new AggregateReference(dataSubjectId, nameof(CustomerProfile)));

			// --- That one tenant erases the data subject ----------------------------------------
			// Filed AND polled inside the erasing tenant's own scope. Under multi-tenancy the erasure store
			// records a request against the AMBIENT tenant — a caller must not be able to file an erasure
			// against a tenant it is not operating as — and a tenant-scoped request is readable only by the
			// tenant that owns it, so a status read taken outside the scope reports nothing and looks like a
			// stall.
			ErasureResult filed;
			ErasureStatus? status;
			using (TenantContextHolder.BeginScope(erasing.TenantId))
			{
				filed = await erasureService.RequestErasureAsync(
					new ErasureRequest
					{
						DataSubjectId = dataSubjectId,
						IdType = DataSubjectIdType.UserId,
						RequestedBy = "api/crypto-shred/two-tenants",
						LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
						Scope = ErasureScope.User,

						// Demo only, so the scheduler picks it up now instead of in 72 hours.
						GracePeriodOverride = TimeSpan.Zero,
					},
					ct).ConfigureAwait(false);

				status = await CryptoShredWalkthroughEndpoint
					.WaitForErasureAsync(erasureService, filed.RequestId, ct)
					.ConfigureAwait(false);
			}

			// --- Ask the ledger about each tenant's key generation, and read each record again ---
			// Same subject, same call, one argument different: the handle. So the erasing tenant's `true` is a
			// lookup that found a row and every other tenant's `false` is a lookup that did not, rather than a
			// ledger that answers the same way to everything.
			var outcomes = new List<object>();
			foreach (var row in written)
			{
				var destroyed = await ledger
					.IsGenerationDestroyedAsync(row.Handle, row.Generation, ct)
					.ConfigureAwait(false);

				var after = await DecryptCopyAsync(row.Stored, cryptor, ct).ConfigureAwait(false);

				outcomes.Add(new
				{
					Tenant = row.Label,
					KeyHandle = row.Handle,
					KeyGeneration = row.Generation,
					ThisTenantErased = ReferenceEquals(row, erasing),

					// The stored form, so "encrypted at rest" is visible rather than asserted.
					StoredAtRest = CryptoShredWalkthroughEndpoint.Preview(row.Stored.EmailAddress),

					// Readable before the erasure, so a null afterwards cannot be confused with a record that
					// was never readable in the first place.
					row.EmailAddressBefore,

					// Null for the erasing tenant, and for no other reason than the ledger row beside it: a
					// key that merely could not be found would make this read THROW instead.
					EmailAddressAfter = after.EmailAddress,
					LedgerSaysGenerationDestroyed = destroyed,

					// Unannotated fields are untouched, which is what keeps a record loadable after an
					// erasure.
					after.Subject,
				});
			}

			return Results.Ok(new
			{
				// The collision itself: one value, held by every tenant above.
				SharedDataSubjectId = dataSubjectId,

				// The whole point, in one boolean. Any two handles equal here would mean one key shared
				// between two tenants, and the erasure below would take both tenants' data with it.
				AllKeyHandlesDiffer =
					written.Select(static r => r.Handle).Distinct(StringComparer.Ordinal).Count() == written.Count,

				Erasure = new
				{
					RequestId = filed.RequestId,
					Tenant = erasing.Label,
					Status = status?.Status.ToString(),
				},

				Tenants = outcomes,
			});
		});
	}

	/// <summary>
	/// One tenant's stored record for the shared data subject, with the key it was written under.
	/// </summary>
	/// <param name="Label">The tenant, as the response names it.</param>
	/// <param name="TenantId">The tenant scope to open, or <see langword="null"/> for the untenanted partition.</param>
	/// <param name="Handle">The key handle protecting this tenant's copy.</param>
	/// <param name="Generation">The generation of material provisioned at that handle when the record was written.</param>
	/// <param name="Stored">The record as the database would hold it: ciphertext.</param>
	/// <param name="EmailAddressBefore">What the stored bytes decrypted to before any erasure.</param>
	private sealed record WrittenNote(
		string Label,
		string? TenantId,
		string Handle,
		string Generation,
		SupportNote Stored,
		string? EmailAddressBefore);

	/// <summary>
	/// Writes one tenant's record for a data subject, and reports the key handle and generation it was
	/// encrypted under.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The key is read BEFORE the write, and the generation identifier is why: a destruction takes the
	/// generation with it, and the next ordinary write mints a new one at the same handle — so asking
	/// afterwards would answer truthfully about material this walkthrough is not holding.
	/// </para>
	/// <para>
	/// <see cref="ISubjectKeyManager"/> takes the tenant as an argument, while
	/// <see cref="SubjectFieldCryptor"/> resolves it from the ambient scope. Both are opened to the same
	/// tenant here so they cannot disagree: a different tenant named in the argument would report a handle
	/// the stored envelope does not name. A consumer who only encrypts and decrypts never names a tenant at
	/// all — the argument is needed here solely to print the handle and ask the ledger about it.
	/// </para>
	/// </remarks>
	private static async Task<(string Handle, string Generation, SupportNote Stored)> WriteNoteAsync(
		string? tenantId,
		string dataSubjectId,
		string emailAddress,
		SubjectFieldCryptor cryptor,
		ISubjectKeyManager keyManager,
		CancellationToken cancellationToken)
	{
		using (TenantContextHolder.BeginScope(tenantId))
		{
			// The untenanted partition is spelled by a constant rather than by a null, because the key manager
			// takes the tenant as a required argument: omitting it has to be a compile error rather than a
			// silent default. This is the same value the ambient context resolves outside a scope.
			var subjectKey = await keyManager
				.GetOrCreateKeyAsync(
					new TenantId(tenantId ?? TenantDefaults.DefaultTenantId),
					dataSubjectId,
					RetentionScope.NotInAnAggregate,
					cancellationToken)
				.ConfigureAwait(false);

			var note = new SupportNote
			{
				CustomerId = dataSubjectId,
				FullName = "Erin Vance",
				EmailAddress = emailAddress,
				Subject = "Replacement wing mirror",
			};

			// aggregateType: null — not stored inside an aggregate, so the fields are protected by the
			// subject's own handle in this tenant, which is the one an erasure of this tenant destroys.
			await cryptor.EncryptFieldsAsync(note, aggregateType: null, cancellationToken).ConfigureAwait(false);

			return (subjectKey.KeyId, subjectKey.Generation?.ToString() ?? string.Empty, note);
		}
	}

	/// <summary>
	/// Decrypts a COPY of a stored record, leaving the stored bytes untouched so the same bytes can be read
	/// again after the erasure.
	/// </summary>
	/// <remarks>
	/// Deliberately called outside any tenant scope. The read rebuilds the tenant from the envelope, so a
	/// background replay with no ambient tenant resolves the same key the write used — and a reader's ambient
	/// tenant can never be mistaken for the one the data was written under.
	/// </remarks>
	private static async Task<SupportNote> DecryptCopyAsync(
		SupportNote stored,
		SubjectFieldCryptor cryptor,
		CancellationToken cancellationToken)
	{
		var copy = stored with { };
		await cryptor.DecryptFieldsAsync(copy, cancellationToken).ConfigureAwait(false);
		return copy;
	}
}
