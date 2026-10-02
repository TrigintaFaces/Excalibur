// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Binds the agreement the crypto-shredding guarantee rests on: the two values a destruction ledger is keyed
/// by are the two values a stored envelope carries, and they are the same two values the framework's own read
/// path asks the ledger about.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why agreement is the property, and not correctness of either side alone.</strong>
/// <see cref="IKeyDestructionLedger"/> is keyed on a handle AND a generation together, and a
/// <see langword="true"/> from it means the plaintext behind <em>this envelope</em> is unrecoverable. So a
/// consumer auditing an erasure — or reporting one, or clearing a subject without attempting a decrypt — is
/// correct only if the pair it asks about is the pair that names the material it is holding. Nothing asserted
/// that before this file. Each half was tested; their agreement was assumed.
/// </para>
/// <para>
/// <strong>What was assumed, concretely.</strong> The handle travels from the key authority through the field
/// encryptor's encryption context, then through an encryption provider which writes <em>its own</em> key
/// metadata into the envelope rather than echoing the context value back. That provider is consumer-pluggable.
/// So between the authority that mints a handle and the envelope that names it there are two hops that could
/// silently substitute a different value, and a substitution does not throw: it asks the ledger about a key
/// nothing ever recorded, which answers <see langword="false"/>, and the read then reports NOT ERASED for data
/// that was. The first arm below is what fails when either hop stops carrying the value through.
/// </para>
/// <para>
/// <strong>Every arm runs under an EXPLICIT tenant.</strong> A key handle is tenant-scoped, so an arm that let
/// the tenant default would be resting on the default identity happening to be what the derivation produces
/// for an absent tenant — the kind of coincidence this file exists to remove rather than rely on. The third
/// arm additionally requires two tenants to disagree, which is what stops the first arm being satisfied by a
/// derivation that ignores the tenant entirely: such a derivation keeps both sides equal while collapsing two
/// subjects onto one key.
/// </para>
/// <para>
/// <strong>Public surface only</strong>, for the reason given in
/// <see cref="StoredEnvelopeGenerationIsReadableByAConsumerShould"/>: this assembly is granted internals by no
/// shipping package, so the restriction is enforced by the compiler rather than by the author's care. The
/// substitutions below exploit the documented <c>TryAdd</c> semantics of the registration extensions — a
/// consumer registering first wins — and not any test-only seam.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
[Trait("Component", "Compliance")]
public sealed class LedgerQuestionIsAnsweredFromTheEnvelopeShould
{
	private const string SubjectId = "subject-ada";
	private const string Plaintext = "ada@example.com";
	private const string TenantAlpha = "tenant-alpha";
	private const string TenantBeta = "tenant-beta";

	// The hashing pepper is required and has a documented minimum length; this is a test value, not a secret.
	private const string Pepper = "smoke-test-pepper-not-a-secret-0123456789abcdef";

	/// <summary>
	/// AGREEMENT. The handle and generation the key authority minted for this subject are the handle and
	/// generation the stored envelope names — across the field encryptor's context and the encryption
	/// provider's own key metadata.
	/// </summary>
	[Fact]
	public async Task Agree_with_the_key_authority_on_both_values_the_ledger_is_keyed_by()
	{
		using var container = await BuildContainerAsync(TenantAlpha, new RecordingLedger()).ConfigureAwait(false);
		using var scope = container.CreateScope();

		var stored = await EncryptThroughTheFrameworkAsync(scope.ServiceProvider).ConfigureAwait(false);
		var envelope = ReadEnvelope(stored);

		// The authority, asked the way the write path asks it. GetOrCreate, so this resolves the key the
		// write already provisioned rather than minting a second one.
		var subjectKey = await scope.ServiceProvider
			.GetRequiredService<ISubjectKeyManager>()
			.GetOrCreateKeyAsync(
				new TenantId(TenantAlpha),
				SubjectId,
				RetentionScope.NotInAnAggregate,
				CancellationToken.None)
			.ConfigureAwait(false);

		envelope.KeyId.ShouldBe(
			subjectKey.KeyId,
			"the envelope must name the handle the key authority minted. If it names a different one, every "
			+ "ledger question asked about this envelope is asked about a key nothing created — which answers "
			+ "false, and a destroyed subject is then reported as live. The substitution does not throw, so "
			+ "this assertion is the only thing that observes it.");

		envelope.KeyGeneration.ShouldBe(
			subjectKey.Generation?.ToString(),
			"the envelope must name the generation the key authority minted. The ledger is keyed on the pair, "
			+ "so a correct handle with the wrong generation fails exactly as a wrong handle does.");
	}

	/// <summary>
	/// NON-VACUITY of the arm above, and the reason reading the envelope beats reconstructing it. Asking the
	/// authority under a tenant the write did not use yields a handle the envelope does not name — silently.
	/// </summary>
	/// <remarks>
	/// This is the failure a consumer reconstructing the pair actually hits, and it is why the agreement arm
	/// is not vacuous: that arm compares two values which this arm shows are easy to make differ. A
	/// reconstruction needs a tenant, cannot read one from the envelope, and gets no error when it picks the
	/// wrong one — the handle simply names material nothing destroyed, the ledger answers "not destroyed", and
	/// a destroyed subject reports as live. Reading the pair off the envelope removes the parameter that can be
	/// wrong rather than documenting how to get it right.
	/// </remarks>
	[Fact]
	public async Task Yield_a_handle_the_envelope_does_not_name_when_the_authority_is_asked_under_another_tenant()
	{
		using var container = await BuildContainerAsync(TenantAlpha, new RecordingLedger()).ConfigureAwait(false);
		using var scope = container.CreateScope();

		var envelope = ReadEnvelope(
			await EncryptThroughTheFrameworkAsync(scope.ServiceProvider).ConfigureAwait(false));

		var wrongTenantKey = await scope.ServiceProvider
			.GetRequiredService<ISubjectKeyManager>()
			.GetOrCreateKeyAsync(
				new TenantId(TenantBeta),
				SubjectId,
				RetentionScope.NotInAnAggregate,
				CancellationToken.None)
			.ConfigureAwait(false);

		wrongTenantKey.KeyId.ShouldNotBe(
			envelope.KeyId,
			"a reconstruction under the wrong tenant must not coincide with the envelope's handle. If these "
			+ "matched, the agreement arm would be satisfied by any tenant at all and would be proving "
			+ "nothing -- and worse, a consumer guessing a tenant would be right by accident, which is the "
			+ "coincidence this whole file exists to replace with a read.");
	}

	/// <summary>
	/// DIRECTION. The framework's own read path asks the ledger with the values the envelope carries, so a
	/// consumer that reads those values asks the same question the framework asks — rather than reconstructing
	/// a question and hoping it matches.
	/// </summary>
	[Fact]
	public async Task Ask_the_ledger_with_the_values_the_envelope_carries_not_a_re_derivation()
	{
		var ledger = new RecordingLedger();
		using var container = await BuildContainerAsync(TenantAlpha, ledger).ConfigureAwait(false);
		using var scope = container.CreateScope();

		var stored = await EncryptThroughTheFrameworkAsync(scope.ServiceProvider).ConfigureAwait(false);

		// Read the pair BEFORE decrypting — the order the ledger's own contract prescribes, and necessary
		// here because a successful decrypt replaces the stored envelope with the plaintext.
		var envelope = ReadEnvelope(stored);

		await scope.ServiceProvider
			.GetRequiredService<SubjectFieldCryptor>()
			.DecryptFieldsAsync(stored, CancellationToken.None)
			.ConfigureAwait(false);

		ledger.Questions.Count.ShouldBe(
			1,
			"premise of this arm: the read path must consult the ledger exactly once for the one personal-data "
			+ "field. If it consulted it zero times the assertion below would compare nothing, and a read that "
			+ "never asks cannot report an erasure at all.");

		ledger.Questions[0].ShouldBe(
			(envelope.KeyId, envelope.KeyGeneration),
			"the framework must ask the ledger with the pair the ENVELOPE carries. That is what makes the "
			+ "public read of the envelope sufficient for a consumer: the question they can now construct is "
			+ "the question the framework constructs. If the read path ever asked using a handle re-derived "
			+ "from the subject instead, a consumer following the envelope would be asking about different "
			+ "material than the framework was, and the two would disagree about whether a subject is erased.");
	}

	/// <summary>
	/// DISCRIMINATION. Two tenants sharing one subject identifier get different handles, so the agreement
	/// above cannot be satisfied by a derivation that drops the tenant.
	/// </summary>
	[Fact]
	public async Task Name_a_different_handle_for_one_subject_under_two_tenants()
	{
		using var alpha = await BuildContainerAsync(TenantAlpha, new RecordingLedger()).ConfigureAwait(false);
		using var beta = await BuildContainerAsync(TenantBeta, new RecordingLedger()).ConfigureAwait(false);
		using var alphaScope = alpha.CreateScope();
		using var betaScope = beta.CreateScope();

		var alphaEnvelope = ReadEnvelope(
			await EncryptThroughTheFrameworkAsync(alphaScope.ServiceProvider).ConfigureAwait(false));
		var betaEnvelope = ReadEnvelope(
			await EncryptThroughTheFrameworkAsync(betaScope.ServiceProvider).ConfigureAwait(false));

		betaEnvelope.KeyId.ShouldNotBe(
			alphaEnvelope.KeyId,
			"one subject identifier supplied by two tenants must not address one key. If it does, either "
			+ "tenant's erasure destroys the other's data, and the ledger row written by one reports the "
			+ "other's live data as lawfully erased. This arm also keeps the agreement arm honest: a "
			+ "derivation that ignored the tenant would keep the authority and the envelope agreeing while "
			+ "collapsing the two subjects, and the agreement arm alone would stay green.");
	}

	/// <summary>
	/// Composes the real crypto-shredding stack — real key authority, real field encryptor, real encryption
	/// provider — through public registration only, substituting two services the registration itself declares
	/// substitutable via <c>TryAdd</c>.
	/// </summary>
	private static async Task<ServiceProvider> BuildContainerAsync(string tenantId, RecordingLedger ledger)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Registered BEFORE AddCryptoShredding, which TryAdds a default tenant context and leaves the ledger
		// to an erasure store. Registering first is the documented way a consumer supplies its own, and is
		// what lets this arm name a tenant explicitly and observe the question the read path asks.
		_ = services.AddSingleton<ITenantContext>(new FixedTenantContext(tenantId));
		_ = services.AddSingleton<IKeyDestructionLedger>(ledger);

		_ = services.Configure<DataSubjectHashingOptions>(o => o.Pepper = Pepper);

		_ = services.AddEncryption(encryption => encryption
			.UseInMemoryKeyManagement("smoke-inmemory", o => o.AutoGenerateDefaultKey = true)
			.SetAsPrimary("smoke-inmemory"));

		_ = services.AddCryptoShredding();

		var provider = services.BuildServiceProvider();

		// START the hosted services, because a container that is merely built is not a host. The primary
		// encryption provider is selected by one of them, and this package's crypto-shredding registration
		// installs a start-up gate that refuses a composition with no way to state a destruction. Starting
		// them means these arms run against a composition the framework itself accepted, rather than one
		// that skipped its own admission checks.
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
		}

		return provider;
	}

	private static async Task<Customer> EncryptThroughTheFrameworkAsync(IServiceProvider provider)
	{
		var record = new Customer { SubjectId = SubjectId, Email = Plaintext };

		await provider
			.GetRequiredService<SubjectFieldCryptor>()
			.EncryptFieldsAsync(record, aggregateType: null, CancellationToken.None)
			.ConfigureAwait(false);

		return record;
	}

	/// <summary>Reads the stored field back into an envelope through the public surface, and nothing else.</summary>
	private static EncryptedData ReadEnvelope(Customer stored)
	{
		var property = typeof(Customer).GetProperty(nameof(Customer.Email))!;

		EncryptedFieldBinding.TryReadEnvelope(property, stored, out var framed).ShouldBeTrue(
			"premise: the framework must have written a readable envelope into the field.");

		EncryptedData.TryParse(framed, out var envelope).ShouldBeTrue(
			"premise: a stored envelope the framework wrote must parse through the public surface.");

		return envelope;
	}

	/// <summary>
	/// Answers "nothing is destroyed" and records every pair it was asked about, so an arm can compare the
	/// question the framework constructed against the envelope a consumer can read. Always false, because
	/// these arms are about which pair is asked, not about what the answer should be.
	/// </summary>
	private sealed class RecordingLedger : IKeyDestructionLedger
	{
		private readonly List<(string KeyHandle, string? KeyGeneration)> _questions = [];

		public IReadOnlyList<(string KeyHandle, string? KeyGeneration)> Questions => _questions;

		public ValueTask<bool> IsGenerationDestroyedAsync(
			string keyHandle,
			string keyGeneration,
			CancellationToken cancellationToken)
		{
			_questions.Add((keyHandle, keyGeneration));
			return ValueTask.FromResult(false);
		}

		public Task RecordDestroyedGenerationAsync(
			string keyHandle,
			string keyGeneration,
			CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId => tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(tenantId);
	}

	private sealed class Customer
	{
		[DataSubjectId]
		public string SubjectId { get; set; } = string.Empty;

		[PersonalData]
		public string? Email { get; set; }
	}
}
