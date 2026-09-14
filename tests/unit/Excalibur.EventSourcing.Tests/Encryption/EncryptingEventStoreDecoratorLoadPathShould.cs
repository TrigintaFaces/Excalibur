// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Locks on what the <see cref="EncryptingEventStoreDecorator"/> LOAD path does to the bytes it was handed,
/// and on which failures it is allowed to swallow.
/// </summary>
/// <remarks>
/// <para>
/// Two properties are pinned here, and each has both arms.
/// </para>
/// <para>
/// <b>Byte fidelity.</b> An event type carrying no personal data must come back byte-identical to what the
/// inner store returned (SAFETY): a deserialize-then-reserialize round trip produces bytes that are only
/// EQUIVALENT, and a consumer that hashes, signs or diffs the payload reads the difference as tampering. The
/// arm cannot be satisfied by disabling the decorator, because an event type that DOES carry personal data
/// must still be decrypted on load (LIVENESS).
/// </para>
/// <para>
/// <b>Which faults are swallowed.</b> An event whose type name is not registered was not written through the
/// field-encryption path and is returned unchanged (SAFETY). A genuine deserialization fault on a KNOWN type
/// must surface instead of being handed back as untouched data (LIVENESS) — without that arm a decorator that
/// returns everything unchanged, forever, passes the whole file.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingEventStoreDecoratorLoadPathShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;

	[Fact]
	public async Task ReturnAZeroPersonalDataEventByteIdentically()
	{
		// Deliberately non-canonical JSON: extra spacing and a property order no serializer would re-emit.
		// A round trip through the model normalizes all of it, which is exactly the drift under test.
		var stored = Encoding.UTF8.GetBytes("""{  "Quantity" : 7,   "Sku":"ABC-1" }""");
		var serializer = new TestEventSerializer();
		var decorator = CreateDecorator(StoreReturning(Event("PlainOrderPlaced", stored)), serializer, out _);

		var loaded = await decorator.LoadAsync("agg-1", "Order", Ct);

		loaded.ShouldHaveSingleItem().EventData.ShouldBe(stored,
			"an event type with no [PersonalData] members has nothing to decrypt, so the decorator must hand "
			+ "back the stored bytes rather than a re-serialization of them");
		serializer.DeserializeCalls.ShouldBe(0,
			"the per-type decision is taken from the resolved type, so a zero-personal-data event is never "
			+ "materialized at all");
	}

	[Fact]
	public async Task DecideOncePerTypeRatherThanOncePerEvent()
	{
		var serializer = new TestEventSerializer();
		var events = Enumerable.Range(0, 25)
			.Select(i => Event("PlainOrderPlaced", Encoding.UTF8.GetBytes($$"""{"Quantity":{{i}}}""")))
			.ToList();
		var decorator = CreateDecorator(StoreReturning([.. events]), serializer, out _);

		var loaded = await decorator.LoadAsync("agg-1", "Order", Ct);

		loaded.Count.ShouldBe(25);
		for (var i = 0; i < 25; i++)
		{
			loaded[i].EventData.ShouldBe(events[i].EventData);
		}

		// The plan behind the decision is cached per type, so twenty-five events cost one resolution's worth
		// of reflection and no materialization at all.
		serializer.DeserializeCalls.ShouldBe(0);
		serializer.SerializeCalls.ShouldBe(0);
	}

	[Fact]
	public async Task StillDecryptAnEventThatCarriesPersonalData()
	{
		// LIVENESS for the byte-fidelity arm: a decorator short-circuited to a pure passthrough would satisfy
		// every assertion above and disable field encryption entirely.
		var fieldEncryptor = new ReversibleFieldEncryptor();
		var cryptor = new SubjectFieldCryptor(fieldEncryptor);

		var atRest = new PersonalOrderPlaced { SubjectId = "subject-1", CustomerName = "Ada Lovelace" };
		await cryptor.EncryptFieldsAsync(atRest, Ct);
		atRest.CustomerName.ShouldNotBe("Ada Lovelace", "the fixture must actually put ciphertext at rest");

		var serializer = new TestEventSerializer();
		var storedBytes = serializer.SerializeEvent(atRest);
		var decorator = CreateDecorator(
			StoreReturning(Event("PersonalOrderPlaced", storedBytes)), serializer, out _, cryptor);

		var loaded = await decorator.LoadAsync("agg-1", "Order", Ct);

		var plaintext = (PersonalOrderPlaced)serializer.DeserializeEvent(
			loaded.ShouldHaveSingleItem().EventData!, typeof(PersonalOrderPlaced));
		plaintext.CustomerName.ShouldBe("Ada Lovelace",
			"a type declaring [PersonalData] must still be materialized and decrypted on load");
	}

	[Fact]
	public async Task ReturnAnEventWhoseTypeIsNotRegisteredUnchanged()
	{
		var stored = Encoding.UTF8.GetBytes("""{"anything":1}""");
		var serializer = new TestEventSerializer();
		var decorator = CreateDecorator(StoreReturning(Event("Some.Retired.Event", stored)), serializer, out _);

		var loaded = await decorator.LoadAsync("agg-1", "Order", Ct);

		loaded.ShouldHaveSingleItem().EventData.ShouldBe(stored,
			"an event whose type name is no longer registered was not written through this decorator's "
			+ "field-encryption path, so there is nothing here to decrypt and the load must not fail");
	}

	[Fact]
	public async Task SurfaceADeserializationFaultOnAKnownType()
	{
		// The fault that must NOT be swallowed. Before this lock the broad catch turned a corrupt payload
		// into "returned unchanged", which is indistinguishable from an unregistered type and hands the
		// caller undecrypted bytes with no diagnostic anywhere.
		var serializer = new TestEventSerializer();
		var decorator = CreateDecorator(
			StoreReturning(Event("PersonalOrderPlaced", Encoding.UTF8.GetBytes("not json at all"))),
			serializer,
			out _);

		_ = await Should.ThrowAsync<JsonException>(async () => await decorator.LoadAsync("agg-1", "Order", Ct));
	}

	private static EncryptingEventStoreDecorator CreateDecorator(
		IEventStore inner,
		IEventSerializer serializer,
		out IEncryptionProviderRegistry registry,
		SubjectFieldCryptor? cryptor = null)
	{
		registry = A.Fake<IEncryptionProviderRegistry>();
		var options = Options.Create(new EncryptionOptions
		{
			Mode = EncryptionMode.EncryptAndDecrypt,
			DefaultPurpose = "test",
			DefaultTenantId = "tenant-1",
		});

		return new EncryptingEventStoreDecorator(
			inner, registry, cryptor ?? new SubjectFieldCryptor(new ReversibleFieldEncryptor()), serializer, options);
	}

	private static IEventStore StoreReturning(params StoredEvent[] events)
	{
		var store = A.Fake<IEventStore>();
		A.CallTo(() => store.LoadAsync("agg-1", "Order", Ct)).Returns(events);
		return store;
	}

	private static StoredEvent Event(string eventType, byte[] data) =>
		new("evt-1", "agg-1", "Order", eventType, data, null, 1, DateTimeOffset.UtcNow);

	/// <summary>
	/// A real serializer over the two fixture events. Implements <see cref="IEventSerializer"/> directly so
	/// the locks bind the interface's contract — in particular that <c>ResolveType</c> THROWS on an
	/// unregistered name rather than returning null — instead of a fake's defaults.
	/// </summary>
	private sealed class TestEventSerializer : IEventSerializer
	{
		public int DeserializeCalls { get; private set; }

		public int SerializeCalls { get; private set; }

		public byte[] SerializeEvent(IDomainEvent domainEvent)
		{
			SerializeCalls++;
			return JsonSerializer.SerializeToUtf8Bytes(domainEvent, domainEvent.GetType());
		}

		public IDomainEvent DeserializeEvent(byte[] data, Type eventType)
		{
			DeserializeCalls++;
			return (IDomainEvent)JsonSerializer.Deserialize(data, eventType)!;
		}

		public string GetTypeName(Type type) => type.Name;

		public Type ResolveType(string typeName) => typeName switch
		{
			"PlainOrderPlaced" => typeof(PlainOrderPlaced),
			"PersonalOrderPlaced" => typeof(PersonalOrderPlaced),
			_ => throw new UnknownEventTypeException($"Cannot resolve event type '{typeName}'."),
		};
	}

	/// <summary>A field encryptor whose ciphertext is recoverable, so a round trip is observable in a unit test.</summary>
	private sealed class ReversibleFieldEncryptor : IFieldEncryptor
	{
		public ValueTask<EncryptedData> EncryptAsync(string subjectId, ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken) =>
			ValueTask.FromResult(new EncryptedData
			{
				Ciphertext = [.. plaintext.ToArray().Select(static b => (byte)(b ^ 0x5A))],
				KeyId = subjectId,
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [1, 2, 3, 4],
			});

		public ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, CancellationToken cancellationToken) =>
			ValueTask.FromResult<byte[]?>([.. envelope.Ciphertext.Select(static b => (byte)(b ^ 0x5A))]);
	}

	[MessageName("Test.EncryptingDecoratorLoadPath.PlainOrderPlaced")]
	private sealed class PlainOrderPlaced : IDomainEvent
	{
		public string Sku { get; set; } = string.Empty;

		public int Quantity { get; set; }

		public string EventId { get; set; } = "evt-1";

		public DateTimeOffset OccurredAt { get; set; }

		[System.Text.Json.Serialization.JsonIgnore]
		public IDictionary<string, object>? Metadata { get; set; }
	}

	[MessageName("Test.EncryptingDecoratorLoadPath.PersonalOrderPlaced")]
	private sealed class PersonalOrderPlaced : IDomainEvent
	{
		[DataSubjectId]
		public string SubjectId { get; set; } = string.Empty;

		[PersonalData]
		public string? CustomerName { get; set; }

		public string EventId { get; set; } = "evt-1";

		public DateTimeOffset OccurredAt { get; set; }

		[System.Text.Json.Serialization.JsonIgnore]
		public IDictionary<string, object>? Metadata { get; set; }
	}
}
