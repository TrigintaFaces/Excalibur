// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Encryption.Decorators;
using Excalibur.EventSourcing.InMemory;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>
/// Author≠impl WIRE + real-crypto round-trip lock for event-store PII encryption, resolved through the
/// REAL keyed repository path. Proves that <c>AddEventSourcingCryptoShredding()</c> wires the
/// <see cref="EncryptingEventStoreDecorator"/> into the event store that consumers actually resolve —
/// the keyed <c>"default"</c> <see cref="IEventStore"/> (<c>GetRequiredKeyedService&lt;IEventStore&gt;("default")</c>),
/// which is how every repository/consumer resolves it — not merely the non-keyed interface. It also proves
/// a <c>[PersonalData]</c> event field is encrypted at rest and decrypted on load via the data subject's
/// key, so destroying that key renders the stored PII unrecoverable (GDPR crypto-shred).
/// </summary>
/// <remarks>
/// Runs against the REAL encryption stack (real AES-GCM over a real in-memory key provider) + a real
/// in-memory event store — no mocks (verify-against-real-infra: a mocked field-encryptor would certify a
/// non-encrypting decorator). The keyed <c>"default"</c> resolve is load-bearing: a decoration step that
/// re-registers the decorator NON-keyed satisfies a non-keyed <c>GetRequiredService&lt;IEventStore&gt;()</c>
/// while leaving the keyed <c>"default"</c> path (the repository's real path) pointing at the un-decorated
/// store — plaintext PII, decorator unreachable. RED-on-mutant: a non-key-preserving
/// <c>DecorateEventStore</c> ⇒ the keyed <c>"default"</c> resolve returns the bare store or fails ⇒ the
/// resolve-is-decorator fact and the at-rest-ciphertext fact both go RED. GREEN only on the keyed-preserving
/// (<c>DescribeKeyed</c>) wiring that mirrors <c>DecorateProjectionStore</c>.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
public sealed class EventStoreEncryptionWiringShould
{
	private const string SubjectId = "subject-42";
	private const string Pii = "SENSITIVE-pii-payload-value";

	private static ServiceProvider BuildWiredStack()
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

		// Event store + at-rest field encryption WIRE under test.
		// The encrypting decorator serializes/deserializes events via IEventSerializer; register it (with
		// assembly scan so the decorator's load-path can resolve the event type from its stored name).
		_ = services.AddSingleton<IEventSerializer>(new JsonEventSerializer(allowAssemblyScan: true));
		_ = services.AddInMemoryEventStore();
		_ = services.AddEventSourcingCryptoShredding();

		var provider = services.BuildServiceProvider();
		// Resolving the encryption providers runs their registration factories, which SELF-register into
		// the IEncryptionProviderRegistry (AesGcmEncryptionProvider's factory calls
		// registry.Register(providerId, provider)). The host normally triggers this via an
		// IEncryptionProviderInitializer; here we force it by resolving them. Registering them again by
		// hand would throw "Provider 'test' is already registered" — the pre-existing bug that left this
		// WIRE lock inert (BuildWiredStack threw before any assertion ran).
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		var registry = provider.GetRequiredService<IEncryptionProviderRegistry>();
		registry.SetPrimary("test");
		return provider;
	}

	[Fact]
	public void ResolvedEventStore_IsTheEncryptingDecorator()
	{
		using var provider = BuildWiredStack();

		// Resolve via the REAL repository path: repositories/consumers resolve the event store as the
		// keyed "default" service (GetRequiredKeyedService<IEventStore>("default")), never the non-keyed
		// interface. A non-keyed resolve is the masking bug — DecorateEventStore that re-registers the
		// decorator NON-keyed satisfies GetRequiredService<IEventStore>() while the keyed "default" path
		// (the one the repository actually uses) is left pointing at the un-decorated store or fails to
		// resolve. Binding the keyed key makes the lock RED on that bug and GREEN on the keyed-preserving fix.
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		// ADR-336 WIRE proof: real DI resolves the decorator through the keyed repository path.
		_ = store.ShouldBeOfType<EncryptingEventStoreDecorator>();
	}

	[Fact]
	public async Task EncryptPiiAtRest_AndDecryptOnLoad_ThroughTheWiredDecorator()
	{
		using var provider = BuildWiredStack();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var evt = new SubjectRegistered
		{
			AggregateId = "agg-1",
			Version = 0,
			SubjectId = SubjectId,
			FullName = Pii,
		};

		_ = await store.AppendAsync("agg-1", "Subject", new IDomainEvent[] { evt }, -1, CancellationToken.None);

		// At rest: the RAW inner store must NOT contain the plaintext PII (it was encrypted before persist).
		var inner = provider.GetRequiredService<InMemoryEventStore>();
		var stored = await inner.LoadAsync("agg-1", "Subject", CancellationToken.None);
		stored.Count.ShouldBe(1);
		var atRest = System.Text.Encoding.UTF8.GetString(stored[0].EventData);
		atRest.Contains(Pii, StringComparison.Ordinal).ShouldBeFalse(
			"the [PersonalData] field must be encrypted at rest — plaintext PII must not appear in the stored payload");

		// On load through the decorator: the PII decrypts back to plaintext (round-trip).
		var serializer = provider.GetRequiredService<IEventSerializer>();
		var loaded = await store.LoadAsync("agg-1", "Subject", CancellationToken.None);
		loaded.Count.ShouldBe(1);
		var roundTripped = (SubjectRegistered)serializer.DeserializeEvent(loaded[0].EventData, typeof(SubjectRegistered));
		roundTripped.FullName.ShouldBe(Pii, "the PII must decrypt back to plaintext on load via the subject key");
	}

	[Fact]
	public async Task CryptoShred_RendersStoredPiiUnrecoverable_AfterKeyDestroyed()
	{
		using var provider = BuildWiredStack();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");

		var evt = new SubjectRegistered
		{
			AggregateId = "agg-2",
			Version = 0,
			SubjectId = SubjectId,
			FullName = Pii,
		};
		_ = await store.AppendAsync("agg-2", "Subject", new IDomainEvent[] { evt }, -1, CancellationToken.None);

		// Crypto-shred: destroy the subject's key. The SAME stored ciphertext is now undecryptable.
		var subjectKeys = provider.GetRequiredService<ISubjectKeyManager>();
		await subjectKeys.DestroyKeyAsync(SubjectId, CancellationToken.None);

		var serializer = provider.GetRequiredService<IEventSerializer>();
		var loaded = await store.LoadAsync("agg-2", "Subject", CancellationToken.None);
		loaded.Count.ShouldBe(1);
		var afterShred = (SubjectRegistered)serializer.DeserializeEvent(loaded[0].EventData, typeof(SubjectRegistered));
		afterShred.FullName.ShouldBeNull(
			"after the subject key is destroyed the PII must be unrecoverable (degrade-open tombstone), not the plaintext");
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
