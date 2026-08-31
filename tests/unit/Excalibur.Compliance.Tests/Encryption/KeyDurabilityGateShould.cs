// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the fail-closed contract for key-material durability: encryption never runs on keys that vanish
/// with the process unless the host asked for exactly that.
/// </summary>
/// <remarks>
/// This is the severe member of the family. Losing an audit trail loses the record of what happened;
/// losing key material makes every value encrypted under it permanently unreadable, with no repair
/// available afterwards and every encrypt call having already returned success.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyDurabilityGateShould
{
	// ---------- SAFETY ----------

	[Fact]
	public void Refuse_a_volatile_key_provider_the_host_never_asked_for()
	{
		using var provider = BuildHost(volatileKeysAccepted: null, durable: false);

		_ = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"silence must not be read as accepting keys that disappear on restart.");
	}

	[Fact]
	public void Say_what_is_lost_and_name_both_remedies()
	{
		using var provider = BuildHost(volatileKeysAccepted: null, durable: false);

		var error = Should.Throw<OptionsValidationException>(() => Resolve(provider));

		// The consequence is the part an operator needs; "invalid configuration" would not convey that
		// continuing costs them their ciphertext.
		error.Message.ShouldContain("unrecoverab", Case.Insensitive);
		error.Message.ShouldContain(nameof(KeyDurabilityOptions.AllowVolatileKeyProvider));
	}

	[Fact]
	public void Default_the_volatile_allowance_to_the_protective_value() =>
		// The unsafe state must not be reachable by omission.
		new KeyDurabilityOptions().AllowVolatileKeyProvider.ShouldBeFalse();

	// ---------- LIVENESS ----------

	[Fact]
	public void Start_when_a_durable_provider_is_registered_through_the_attesting_seam()
	{
		using var provider = BuildHost(volatileKeysAccepted: null, durable: true);

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Start_when_the_host_accepts_volatile_keys_deliberately()
	{
		using var provider = BuildHost(volatileKeysAccepted: true, durable: false);

		Should.NotThrow(
			() => Resolve(provider),
			"a development host may choose volatile keys; the gate governs silence, not choice.");
	}

	[Fact]
	public async Task Keep_key_material_readable_across_a_simulated_restart()
	{
		// The liveness arm the safety arms cannot supply: a gate that admitted only providers which store
		// nothing would pass every arm above. Two independent hosts share one durable backing store, which
		// is what "survives a restart" means operationally.
		var backingStore = new ConcurrentDictionary<string, KeyMetadata>();

		using (var before = BuildHost(volatileKeysAccepted: null, durable: true, backingStore))
		{
			var keys = before.GetRequiredService<IKeyManagementProvider>();
			_ = await keys.RotateKeyAsync(
				"tenant-key",
				EncryptionAlgorithm.Aes256Gcm,
				purpose: null,
				expiresAt: null,
				CancellationToken.None);
		}

		using var after = BuildHost(volatileKeysAccepted: null, durable: true, backingStore);

		var recovered = await after.GetRequiredService<IKeyManagementProvider>()
			.GetKeyAsync("tenant-key", CancellationToken.None);

		recovered.ShouldNotBeNull(
			"a durable provider must still hand back the key after the process that created it is gone.");
	}

	// ---------- WIRING ----------

	[Fact]
	public void Not_answer_the_durability_capability_for_a_volatile_provider()
	{
		// Retargeted from the deleted internal marker to the public capability query: a volatile provider
		// must answer null so the validator can distinguish it from a durable one.
		IKeyManagementProvider volatileProvider = new FakeVolatileKeyProvider();

		volatileProvider.GetService(typeof(IDurableKeyProvider)).ShouldBeNull(
			"a volatile provider answering the durability capability would be the false safety this gate removes.");
	}

	[Fact]
	public void Be_wired_by_the_registration_path_not_only_by_this_test()
	{
		using var provider = BuildHost(volatileKeysAccepted: true, durable: false);

		provider.GetServices<IValidateOptions<KeyDurabilityOptions>>()
			.ShouldContain(v => v is KeyDurabilityValidator);
	}

	// ---------- PRODUCTION-PATH WIRING ----------
	//
	// The arms above build their own host and call AddKeyDurabilityGate() directly, so they prove the gate
	// WORKS while proving nothing about whether the shipped registration paths INSTALL it. Verified the
	// hard way: with the gate call deleted from each production site in turn, every arm above still passed.
	// The arms below close that hole — each drives a real consumer entry point and asserts the gate arrived.
	//
	// Each was verified by deleting its site's gate call, rebuilding, and confirming exactly that arm went
	// RED while the other thirteen stayed GREEN:
	//   AddComplianceEncryption -> ComplianceEncryptionBuilder.Build()
	//   AddEncryption           -> EncryptionServiceCollectionExtensions.AddEncryption
	//   AddGdprErasure          -> ErasureServiceCollectionExtensions.RegisterGdprErasureCore
	//
	// Each asserts the gate's EFFECT — a volatile provider is refused when the options resolve — and not
	// that a validator is present in the collection. Presence is satisfied by a validator that refuses
	// nothing; the refusal is what the entry point owes the consumer. Each safety arm below is paired with
	// a liveness arm on the same entry point, because a gate that refused every composition would satisfy
	// the safety arm alone while making the entry point unusable.

	[Fact]
	public void Fail_closed_through_AddComplianceEncryption_when_no_key_management_was_chosen()
	{
		// Site 1: ComplianceEncryptionBuilder.Build(). The bare builder selects no key management, so the
		// in-memory provider wins registration and nothing sets AllowVolatileKeyProvider. This is the
		// composition a consumer reaches by following the documented entry point without thinking about
		// durability — precisely the host the gate exists for.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddComplianceEncryption(builder => builder.WithEncryption());

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"AddComplianceEncryption must install the gate: a host composing encryption through this entry "
			+ "point gets no later chance to learn its key material is volatile.");

		error.Message.ShouldContain("unrecoverab", Case.Insensitive);
	}

	[Fact]
	public void Start_through_AddComplianceEncryption_when_in_memory_keys_were_chosen_deliberately()
	{
		// Liveness for the arm above: WithInMemoryKeyManagement IS the host stating it accepts volatile
		// keys, and the gate this entry point installs must admit that.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddComplianceEncryption(builder => builder.WithInMemoryKeyManagement());

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Fail_closed_through_AddEncryption_when_the_host_brings_a_volatile_key_provider()
	{
		// Site 2: EncryptionServiceCollectionExtensions.AddEncryption. A consumer bringing their own key
		// management: UseInMemoryKeyManagement would have BEEN the explicit acceptance of volatile keys,
		// and this path deliberately is not it. The provider registered here answers null for
		// IDurableKeyProvider, which is the signal the gate reads.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IKeyManagementProvider, FakeVolatileKeyProvider>();
		_ = services.AddEncryption(
			encryption => encryption.UseKeyManagement<AesGcmEncryptionProvider>("byo-key-management"));

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"AddEncryption must install the gate; otherwise a host that never stated a durability intention "
			+ "encrypts under keys that vanish with the process.");

		error.Message.ShouldContain("unrecoverab", Case.Insensitive);
	}

	[Fact]
	public void Start_through_AddEncryption_when_in_memory_key_management_was_selected()
	{
		// Liveness for the arm above. AddDevEncryption rides this same path, so a gate that refused here
		// would break the documented development entry point.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddEncryption(encryption => encryption.UseInMemoryKeyManagement("dev-inmemory"));

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}

	[Fact]
	public void Fail_closed_through_AddGdprErasure_on_its_default_key_management()
	{
		// Site 3: ErasureServiceCollectionExtensions.RegisterGdprErasureCore. Crypto-shred erasure works by
		// destroying the key, so this is the composition where volatile keys are most misleading: the keys
		// are gone on restart regardless, and a shred over keys already lost cannot be attested. The entry
		// point TryAdds the in-memory provider and sets no opt-out.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddGdprErasure();

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(
			() => Resolve(provider),
			"AddGdprErasure must install the gate; an erasure certificate issued over keys that vanish on "
			+ "restart attests nothing.");

		error.Message.ShouldContain(nameof(KeyDurabilityOptions.AllowVolatileKeyProvider));
	}

	[Fact]
	public void Start_through_AddGdprErasure_when_a_durable_key_provider_is_registered()
	{
		// Liveness for the arm above, on the composition a production host actually has: a durable provider
		// registered ahead of the entry point's TryAdd fallback, which therefore does not win.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new ConcurrentDictionary<string, KeyMetadata>());
		_ = services.AddSingleton<IKeyManagementProvider, FakeDurableKeyProvider>();
		_ = services.AddGdprErasure();

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => Resolve(provider));
	}
	private static ServiceProvider BuildHost(
		bool? volatileKeysAccepted,
		bool durable,
		ConcurrentDictionary<string, KeyMetadata>? backingStore = null)
	{
		var services = new ServiceCollection();
		_ = services.AddKeyDurabilityGate();

		if (durable)
		{
			var store = backingStore ?? new ConcurrentDictionary<string, KeyMetadata>();
			_ = services.AddSingleton(store);
			_ = services.AddSingleton<IKeyManagementProvider, FakeDurableKeyProvider>();
		}

		if (volatileKeysAccepted is bool accepted)
		{
			_ = services.Configure<KeyDurabilityOptions>(o => o.AllowVolatileKeyProvider = accepted);
		}

		return services.BuildServiceProvider();
	}

	private static KeyDurabilityOptions Resolve(IServiceProvider provider) =>
		provider.GetRequiredService<IOptions<KeyDurabilityOptions>>().Value;

	/// <summary>
	/// A durable provider stand-in implementing <see cref="IKeyManagementProvider" /> directly, backed by a
	/// store handed in from outside its own lifetime — so a new instance sees what a previous one wrote,
	/// which is the property "durable" actually names.
	/// </summary>
	private sealed class FakeDurableKeyProvider : IKeyManagementProvider, IDurableKeyProvider
	{
		private readonly ConcurrentDictionary<string, KeyMetadata> _store;

		public FakeDurableKeyProvider(ConcurrentDictionary<string, KeyMetadata> store) => _store = store;

		public Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken) =>
			Task.FromResult(_store.TryGetValue(keyId, out var found) ? found : null);

		public Task<KeyMetadata?> GetKeyVersionAsync(
			string keyId,
			int version,
			CancellationToken cancellationToken) =>
			GetKeyAsync(keyId, cancellationToken);

		public Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken) =>
			Task.FromResult(_store.Values.FirstOrDefault());

		public Task<KeyRotationResult> RotateKeyAsync(
			string keyId,
			EncryptionAlgorithm algorithm,
			string? purpose,
			DateTimeOffset? expiresAt,
			CancellationToken cancellationToken)
		{
			_ = _store.TryAdd(
				keyId,
				new KeyMetadata
				{
					KeyId = keyId,
					Version = 1,
					Algorithm = algorithm,
					Status = KeyStatus.Active,
					CreatedAt = DateTimeOffset.UnixEpoch,
				});

			return Task.FromResult(new KeyRotationResult { Success = true });
		}
	}

	/// <summary>
	/// A volatile provider: implements <see cref="IKeyManagementProvider" /> but NOT
	/// <see cref="IDurableKeyProvider" />, so it answers null for the durability capability. This is the
	/// provider the gate must refuse when the host states no durability intention.
	/// </summary>
	private sealed class FakeVolatileKeyProvider : IKeyManagementProvider
	{
		public Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyRotationResult> RotateKeyAsync(string keyId, EncryptionAlgorithm algorithm,
			string? purpose, DateTimeOffset? expiresAt, CancellationToken cancellationToken) =>
			Task.FromResult(new KeyRotationResult { Success = true });
	}
}
