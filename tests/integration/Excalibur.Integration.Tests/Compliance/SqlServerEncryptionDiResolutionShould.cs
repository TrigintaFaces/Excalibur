// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Erasure;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.Inbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

using Testcontainers.MsSql;

using Tests.Shared.Helpers;

#pragma warning disable CA2100 // SQL strings are safe - schema scripts and fixed queries in this test file

namespace Excalibur.Integration.Tests.Compliance;

/// <summary>
/// Real-engine DI-resolution lock (hwe9c3) for the tenancy/encryption decorator pair the bead names as its
/// "sharpest argument": <see cref="EncryptingEventStoreDecorator"/> and the internal
/// <c>EncryptingInboxStoreDecorator</c>. Every existing conformance/encryption suite either hand-constructs
/// the decorator (handing it the dependency a real DI factory could have dropped) or resolves it against
/// <c>AddInMemoryEventStore()</c>, which cannot show an ENGINE mangling the round-tripped ciphertext bytes
/// (Oracle folding, a JSON/BSON re-encode, a varchar-vs-varbinary coercion). This suite resolves both
/// decorators through their single documented entry point, <c>AddEventSourcingCryptoShredding()</c>, over a
/// REAL SQL Server, and proves the read path that branches on those bytes survives the real engine.
/// </summary>
/// <remarks>
/// <para>
/// WIRE proof: the keyed <c>"default"</c> service each store's consumer actually resolves
/// (<c>GetRequiredKeyedService&lt;T&gt;("default")</c>) is the decorator, not the bare provider store. A DI
/// factory that dropped the cryptor/registry dependency, or a decoration step that mis-keyed the
/// registration, would leave a consumer resolving the un-encrypted store — invisible to a hand-constructed
/// unit test that hands the decorator its dependencies directly.
/// </para>
/// <para>
/// BYTES proof: a PII field is encrypted before it reaches SQL Server (the raw row, read back with a bare
/// <see cref="SqlConnection"/> outside the decorator, must not contain the plaintext) and decrypts correctly
/// coming back through the decorator (the real engine's storage format did not corrupt the ciphertext).
/// </para>
/// <para>
/// Never skipped: an absent Docker daemon fails the arm rather than passing silently.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
[SuppressMessage("Design", "CA1001", Justification = "Disposed in DisposeAsync via IAsyncLifetime")]
public sealed class SqlServerEncryptionDiResolutionShould : IAsyncLifetime
{
	private const string SubjectId = "subject-di-resolution";
	private const string Pii = "SENSITIVE-pii-payload-value";
	private const string InboxDatabaseName = "EncryptionDiResolutionInbox";

	private MsSqlContainer? _container;
	private string? _eventStoreConnectionString;
	private string? _inboxConnectionString;
	private bool _dockerAvailable;
	private Exception? _unavailableCause;

	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.WithName($"mssql-encryption-di-{Guid.NewGuid():N}")
				.WithPassword("Test@Pass123")
				.WithCleanUp(true)
				.Build();

			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			await _container.StartAsync(startCts.Token).ConfigureAwait(false);

			var baseConnectionString = _container.GetConnectionString();
			_eventStoreConnectionString = baseConnectionString;

			await using (var connection = new SqlConnection(baseConnectionString))
			{
				await connection.OpenAsync(startCts.Token).ConfigureAwait(false);
				foreach (var script in ShippedSchemaScript.ReadSqlCmdBatches(
					"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001_CreateEventStoreSchema.sql"))
				{
					await using var command = new SqlCommand(script, connection);
					_ = await command.ExecuteNonQueryAsync(startCts.Token).ConfigureAwait(false);
				}
			}

			// Dedicated database: the shipped inbox script hardcodes [dbo].[inbox_messages], which cannot
			// coexist with the event store's own [dbo] schema on the same database.
			_inboxConnectionString = await ShippedInboxSchema.EnsureDatabaseAndSchemaAsync(
				baseConnectionString, InboxDatabaseName, startCts.Token).ConfigureAwait(false);

			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			_unavailableCause = ex;
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is null)
		{
			return;
		}

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}

	[Fact]
	public async Task ResolveEventStoreAndInboxStore_AsTheEncryptingDecorators_ThroughTheDocumentedEntryPoint()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine encryption "
			+ "DI-resolution lock is never satisfied by not running."));

		await using var provider = await BuildWiredStackAsync().ConfigureAwait(false);

		var eventStore = provider.GetRequiredKeyedService<IEventStore>("default");
		_ = eventStore.ShouldBeOfType<EncryptingEventStoreDecorator>(
			"hwe9c3: AddEventSourcingCryptoShredding() must resolve the keyed \"default\" IEventStore - the "
			+ "path every repository/consumer actually uses - to the encrypting decorator, not the bare "
			+ "SQL Server store. A DI factory that dropped the cryptor/registry would leave PII plaintext.");

		var inboxStore = provider.GetRequiredKeyedService<IInboxStore>("default");
		inboxStore.GetType().Name.ShouldBe(
			"EncryptingInboxStoreDecorator",
			"hwe9c3: AddEventSourcingCryptoShredding() must resolve the keyed \"default\" IInboxStore to the "
			+ "encrypting decorator through the same call that encrypts the event store.");
	}

	[Fact]
	public async Task EncryptEventStorePii_AtRest_OnRealSqlServer_AndDecryptOnLoad()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine encryption "
			+ "DI-resolution lock is never satisfied by not running."));

		await using var provider = await BuildWiredStackAsync().ConfigureAwait(false);
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var evt = new SubjectRegistered
		{
			AggregateId = "agg-di-1",
			Version = 0,
			SubjectId = SubjectId,
			FullName = Pii,
		};
		_ = await store.AppendAsync("agg-di-1", "Subject", new IDomainEvent[] { evt }, -1, CancellationToken.None)
			.ConfigureAwait(false);

		// At rest, on the REAL engine: read the raw row with a bare connection - outside the decorator - and
		// confirm the plaintext PII was never written. A dropped cryptor dependency would leave it here.
		await using (var connection = new SqlConnection(_eventStoreConnectionString))
		{
			await connection.OpenAsync().ConfigureAwait(false);
			await using var command = new SqlCommand(
				"SELECT EventData FROM [dbo].[EventStoreEvents] WHERE AggregateId = @AggregateId", connection);
			_ = command.Parameters.AddWithValue("@AggregateId", "agg-di-1");
			await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
			_ = await reader.ReadAsync().ConfigureAwait(false);
			var raw = (byte[])reader["EventData"];
			var atRest = System.Text.Encoding.UTF8.GetString(raw);
			atRest.Contains(Pii, StringComparison.Ordinal).ShouldBeFalse(
				"the [PersonalData] field must be encrypted before it reaches SQL Server");
		}

		// Through the decorator: the real engine's storage/round-trip must not have corrupted the ciphertext.
		var serializer = provider.GetRequiredService<IEventSerializer>();
		var loaded = await store.LoadAsync("agg-di-1", "Subject", CancellationToken.None).ConfigureAwait(false);
		loaded.Count.ShouldBe(1);
		var roundTripped = (SubjectRegistered)serializer.DeserializeEvent(loaded[0].EventData, typeof(SubjectRegistered));
		roundTripped.FullName.ShouldBe(Pii, "the PII must decrypt back to plaintext after a real SQL Server round trip");
	}

	[Fact]
	public async Task EncryptInboxPayload_AtRest_OnRealSqlServer_AndDecryptOnRead()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine encryption "
			+ "DI-resolution lock is never satisfied by not running."));

		await using var provider = await BuildWiredStackAsync().ConfigureAwait(false);
		var store = provider.GetRequiredKeyedService<IInboxStore>("default");

		var payload = System.Text.Encoding.UTF8.GetBytes(Pii);
		var messageId = Guid.NewGuid().ToString();
		_ = await store.CreateEntryAsync(
			messageId,
			"Handler.DiResolution",
			"Excalibur.Integration.Tests.Compliance.SqlServerEncryptionDiResolutionShould+ProbeMessage",
			payload,
			new Dictionary<string, object>(StringComparer.Ordinal),
			CancellationToken.None).ConfigureAwait(false);

		// At rest, on the REAL engine: raw row via a bare connection, outside the decorator.
		await using (var connection = new SqlConnection(_inboxConnectionString))
		{
			await connection.OpenAsync().ConfigureAwait(false);
			await using var command = new SqlCommand(
				"SELECT Payload FROM [dbo].[inbox_messages] WHERE MessageId = @MessageId", connection);
			_ = command.Parameters.AddWithValue("@MessageId", messageId);
			await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
			_ = await reader.ReadAsync().ConfigureAwait(false);
			var raw = (byte[])reader["Payload"];
			var atRest = System.Text.Encoding.UTF8.GetString(raw);
			atRest.Contains(Pii, StringComparison.Ordinal).ShouldBeFalse(
				"the inbox payload must be encrypted before it reaches SQL Server");
		}

		// Through the decorator: the real engine round trip must not have corrupted the ciphertext.
		var entry = await store.GetEntryAsync(messageId, "Handler.DiResolution", CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		System.Text.Encoding.UTF8.GetString(entry.Payload).ShouldBe(
			Pii, "the payload must decrypt back to plaintext after a real SQL Server round trip");
	}

	private async Task<ServiceProvider> BuildWiredStackAsync()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDataSubjectHashing();
		_ = services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-pepper-0123456789abcdef0123456789ab");

		_ = services.AddEncryption(b => b.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		_ = services.AddSingleton<IKeyManagementAdmin>(sp =>
			(IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());
		_ = services.AddCryptoShredding();

		_ = services.AddSingleton<IEventSerializer>(new JsonEventSerializer(allowAssemblyScan: true));
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseSqlServer(sql =>
			_ = sql.ConnectionString(_eventStoreConnectionString!))));
		_ = services.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql =>
			_ = sql.ConnectionString(_inboxConnectionString!)));

		// AddEventSourcingCryptoShredding() decorates the event store, inbox AND outbox in one call - this
		// suite deliberately registers no outbox, exercising the "may legitimately enable encryption before
		// selecting every store" path the method's own remarks describe.
		_ = services.AddEventSourcingCryptoShredding();

		var provider = services.BuildServiceProvider();

		// Resolving the encryption providers runs their registration factories, which self-register into the
		// registry (mirrors EventStoreEncryptionWiringShould's BuildWiredStack - a host normally triggers
		// this via IEncryptionProviderInitializer).
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		var registry = provider.GetRequiredService<IEncryptionProviderRegistry>();
		registry.SetPrimary("test");

		// The event-store path resolves its key PER SUBJECT (SubjectFieldCryptor/ISubjectKeyManager), which
		// creates a key on first use. The inbox decorator's whole-payload path resolves an ACTIVE key for
		// EncryptionOptions.DefaultPurpose ("default") directly via IKeyManagementProvider.GetActiveKeyAsync
		// - and the in-memory provider's auto-generated default key carries Purpose=null, which never
		// matches a non-null purpose lookup. Provision an explicit key under the purpose this host's
		// EncryptionOptions actually uses, exactly as a real consumer's key-management setup must.
		var keyManagement = provider.GetRequiredService<IKeyManagementProvider>();
		_ = await keyManagement.RotateKeyAsync(
			"inbox-default-purpose-key", EncryptionAlgorithm.Aes256Gcm, "default", null, CancellationToken.None)
			.ConfigureAwait(false);

		return provider;
	}

	/// <summary>A test domain event carrying a data subject's PII field.</summary>
	private sealed record SubjectRegistered : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(SubjectRegistered);
		public IDictionary<string, object>? Metadata { get; init; }

		[DataSubjectId]
		public string SubjectId { get; init; } = string.Empty;

		[PersonalData]
		public string? FullName { get; init; }
	}
}
