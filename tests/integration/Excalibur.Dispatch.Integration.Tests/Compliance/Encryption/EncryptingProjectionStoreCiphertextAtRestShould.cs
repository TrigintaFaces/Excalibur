// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Encryption;

/// <summary>
/// as4sb4 — real-infrastructure ciphertext-at-rest round-trip lock for
/// <see cref="EncryptingProjectionStoreDecorator{TProjection}"/>. The shipped decorator encrypts
/// <c>[EncryptedField] byte[]</c> projection properties on write (prepending the "EXCR" envelope) and
/// decrypts on read. The only prior coverage was MOCK-only unit tests whose inner store never persisted a
/// row — so nothing proved the plaintext is actually absent from the datastore. This lock persists through a
/// REAL <see cref="PostgresProjectionStore{TProjection}"/> (real container, DEFAULT client) wrapped by the
/// decorator over a REAL AES-GCM <see cref="IEncryptionProviderRegistry"/>, then reads the raw row with Dapper.
/// </summary>
/// <remarks>
/// SAFETY (ciphertext-at-rest) is paired with LIVENESS (the decrypt round-trip returns the original bytes) and
/// a NON-VACUITY control (a <see cref="EncryptionMode.Disabled"/> decorator persists the plaintext at rest, so
/// the ciphertext assertion is provably meaningful — it would fail if encryption were inert). Never skip-gated:
/// when Docker is unavailable the fixture assertion fails rather than passing silently.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Database", "Postgres")]
public sealed class EncryptingProjectionStoreCiphertextAtRestShould : IClassFixture<PostgresFixture>, IAsyncLifetime
{
	private const string TableName = "test_encrypted_projection";

	// A distinctive plaintext so its base64 form is unambiguous to search for in the raw datastore row.
	private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("SUPER-SECRET-PII-PLAINTEXT-VALUE-9f3a51");
	private static readonly string PlaintextBase64 = Convert.ToBase64String(Plaintext);

	// The at-rest envelope prefix: EncryptedData.MagicBytes = "EXCR" (0x45 0x58 0x43 0x52). The first three
	// bytes 'E','X','C' base64-encode to "RVhD", so a base64-serialized ciphertext field begins with it.
	private const string EnvelopeBase64Prefix = "RVhD";

	private readonly PostgresFixture _fixture;
	private readonly ILogger<PostgresProjectionStore<EncryptedTestProjection>> _logger;

	public EncryptingProjectionStoreCiphertextAtRestShould(PostgresFixture fixture)
	{
		_fixture = fixture;
		_logger = new LoggerFactory().CreateLogger<PostgresProjectionStore<EncryptedTestProjection>>();
	}

	public async ValueTask InitializeAsync()
	{
		if (!_fixture.DockerAvailable)
		{
			return;
		}

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		// Tenant-scoped projection table: row-level tenant_id with a composite (id, tenant_id) key — the store's
		// UPSERT targets ON CONFLICT (id, tenant_id) so distinct tenants never overwrite each other's rows.
		_ = await connection.ExecuteAsync($"""
			CREATE TABLE IF NOT EXISTS "{TableName}" (
				id VARCHAR(450) NOT NULL,
				tenant_id VARCHAR(450) NOT NULL,
				data JSONB NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (id, tenant_id)
			)
			""").ConfigureAwait(false);
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <summary>
	/// SAFETY (ciphertext-at-rest) + LIVENESS (round-trip): with <see cref="EncryptionMode.EncryptAndDecrypt"/>,
	/// the persisted row must NOT contain the plaintext (it carries the "EXCR" envelope instead), yet a read
	/// through the decorator returns the exact original bytes.
	/// </summary>
	[Fact]
	public async Task EncryptFieldAtRest_AndRoundTripThroughDecrypt()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra ciphertext-at-rest conformance is never skipped.");

		using var encryptionServices = await BuildRealAesRegistryAsync().ConfigureAwait(false);
		var encrypting = Decorate(encryptionServices, EncryptionMode.EncryptAndDecrypt);

		const string Id = "enc-at-rest-1";
		await encrypting.UpsertAsync(
			Id, new EncryptedTestProjection { Id = Id, SensitiveData = Plaintext }, CancellationToken.None).ConfigureAwait(false);

		// SAFETY — the raw datastore row must NOT hold the plaintext, and MUST carry the encryption envelope.
		var rawJson = await ReadRawDataJsonAsync(Id).ConfigureAwait(false);
		rawJson.Contains(PlaintextBase64, StringComparison.Ordinal).ShouldBeFalse(
			"an [EncryptedField] byte[] must be ciphertext at rest — the plaintext must never reach the datastore row.");
		rawJson.Contains(EnvelopeBase64Prefix, StringComparison.Ordinal).ShouldBeTrue(
			"the persisted field must carry the EXCR encryption envelope (base64-prefixed), proving it was encrypted, not merely reordered.");

		// LIVENESS — a read back through the decorator decrypts to the exact original bytes.
		var readBack = await encrypting.GetByIdAsync(Id, CancellationToken.None).ConfigureAwait(false);
		_ = readBack.ShouldNotBeNull("the encrypted projection must be retrievable through the decorator.");
		readBack.SensitiveData.ShouldBe(Plaintext,
			"the decrypt-on-read path must return the original plaintext bytes (round-trip), not the ciphertext.");
	}

	/// <summary>
	/// NON-VACUITY control — a <see cref="EncryptionMode.Disabled"/> decorator writes the plaintext straight
	/// through, so the raw row DOES contain the plaintext base64. This proves the ciphertext assertion above is
	/// meaningful: it would fail (RED) if encryption were inert.
	/// </summary>
	[Fact]
	public async Task PersistPlaintextAtRest_WhenEncryptionDisabled_ProvingTheCiphertextAssertionIsNonVacuous()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra ciphertext-at-rest conformance is never skipped.");

		using var encryptionServices = await BuildRealAesRegistryAsync().ConfigureAwait(false);
		var plaintextMode = Decorate(encryptionServices, EncryptionMode.Disabled);

		const string Id = "plaintext-at-rest-1";
		await plaintextMode.UpsertAsync(
			Id, new EncryptedTestProjection { Id = Id, SensitiveData = Plaintext }, CancellationToken.None).ConfigureAwait(false);

		var rawJson = await ReadRawDataJsonAsync(Id).ConfigureAwait(false);
		rawJson.Contains(PlaintextBase64, StringComparison.Ordinal).ShouldBeTrue(
			"with encryption Disabled the plaintext is persisted at rest — this is the RED control that makes the "
			+ "EncryptAndDecrypt ciphertext-at-rest assertion non-vacuous.");
	}

	private const string TestTenant = "as4sb4-test-tenant";
	private const string DefaultPurpose = "default";

	private EncryptingProjectionStoreDecorator<EncryptedTestProjection> Decorate(
		ServiceProvider encryptionServices, EncryptionMode mode)
	{
		// The projection store scopes rows by tenant (RequireTenant) and the encryption context is tenant-bound;
		// supply a stable tenant so both the persistence and the key-derivation paths resolve one.
		var tenantContext = A.Fake<ITenantContext>();
		_ = A.CallTo(() => tenantContext.TenantId).Returns(TestTenant);

		var inner = new PostgresProjectionStore<EncryptedTestProjection>(
			_fixture.ConnectionString, _logger, tenantContext: tenantContext, tableName: TableName);
		var registry = encryptionServices.GetRequiredService<IEncryptionProviderRegistry>();
		var options = Microsoft.Extensions.Options.Options.Create(new EncryptionOptions { Mode = mode });
		return new EncryptingProjectionStoreDecorator<EncryptedTestProjection>(inner, registry, options);
	}

	// Build a REAL AES-GCM provider registry (in-memory key management = real encryption, ephemeral keys).
	// Resolving the providers runs their registration factories, which self-register into the registry; then
	// provision an active key for the default purpose so the provider can resolve a key at encrypt time.
	private static async Task<ServiceProvider> BuildRealAesRegistryAsync()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));

		var provider = services.BuildServiceProvider();
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		provider.GetRequiredService<IEncryptionProviderRegistry>().SetPrimary("test");

		_ = await provider.GetRequiredService<IKeyManagementProvider>().RotateKeyAsync(
			DefaultPurpose, EncryptionAlgorithm.Aes256Gcm, DefaultPurpose, expiresAt: null, CancellationToken.None)
			.ConfigureAwait(false);

		return provider;
	}

	private async Task<string> ReadRawDataJsonAsync(string id)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<string>(
			$"""SELECT data::text FROM "{TableName}" WHERE id = @id""", new { id }).ConfigureAwait(false)
			?? throw new InvalidOperationException($"row '{id}' was not persisted");
	}

	private sealed class EncryptedTestProjection
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? SensitiveData { get; set; }
	}
}
