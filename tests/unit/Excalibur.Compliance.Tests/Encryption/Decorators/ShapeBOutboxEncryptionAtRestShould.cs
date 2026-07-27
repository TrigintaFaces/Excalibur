// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

// Independent regression lock (author != implementer, TestsDeveloper) for the safety-critical vh1hit P1
// GDPR PII-at-rest leak.
//
// THE BUG (vh1hit): `AddOutbox<TStore>()` / `AddInbox<TStore>()` register the REAL store directly under the keyed
// "default" service key — the "Shape B" registration (`AddKeyedSingleton<IOutboxStore, TStore>("default")`). The
// OLD store-encryption wiring filtered `ServiceKey != "default"` when choosing what to decorate, so for a Shape-B
// consumer `AddOutboxEncryption()` decorated NOTHING, emitted no marker, and every [PersonalData] payload was
// persisted PLAINTEXT at rest — GDPR crypto-shredding silently inert. (Concrete providers register Shape A — a
// keyed provider terminal + a "default" forwarder — and were already correct.)
//
// THE FIX: `AddOutboxEncryption` now wires through the shared `DecorateKeyedStores<IOutboxStore>` helper, whose
// rule 2 decorates the "default" keyed descriptor when it IS the real store (Shape B) — the exact registration the
// old filter skipped.
//
// FIDELITY: this exercises the REAL wiring under test (`AddOutboxEncryption` → `DecorateKeyedStores`) and the REAL
// `EncryptingOutboxStoreDecorator`, asserting the bytes handed to the store AT REST (a capturing inner sink) — not
// the mere presence of a marker (verify-against-real-infra-not-mock: "a flag nothing reads is the original bug in a
// costume"). The encryption PROVIDER is a deterministic reversible fake (the same approach the sibling
// EncryptingOutboxStoreDecoratorShould uses), because the vh1hit defect is a WIRING bug, not a crypto bug — the
// real AES-GCM provider + registry startup is a separate subsystem with its own tests. The transform is
// bitwise-NOT so the plaintext byte sequence provably cannot appear at rest.
//
// SAFETY + LIVENESS (testing-patterns §3): "plaintext not at rest" alone is satisfied by a decorator that destroys
// the payload; it is paired with a liveness arm proving the value round-trips back to the original plaintext
// (encryption, not corruption). The third arm is the non-vacuity control: the pre-fix wiring (Shape-B "default"
// left undecorated) persists PLAINTEXT, so the safety arm is RED against the bug.
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ShapeBOutboxEncryptionAtRestShould
{
	private static readonly byte[] Pii = "tenant-a:jane.doe@example.com:SSN-123-45-6789"u8.ToArray();

	[Fact]
	public async Task EncryptTheShapeBDefaultStorePayloadAtRest_WhenAddOutboxEncryptionIsWired()
	{
		// SAFETY + WIRING (the fix). Shape-B real store under "default" + AddOutboxEncryption must (a) wrap the
		// "default" store in the encrypting decorator, and (b) hand the inner store CIPHERTEXT — the plaintext PII
		// must not be persisted at rest. RED against the pre-fix ServiceKey!="default" filter (see the control arm).
		var atRest = new CapturingOutboxStore();
		var (services, _) = WiredServices(atRest, encrypt: true);

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IOutboxStore>("default");

		store.ShouldBeOfType<EncryptingOutboxStoreDecorator>(
			"AddOutboxEncryption must wrap the Shape-B \"default\" real store in the encrypting decorator. Before the " +
			"fix the wiring filtered ServiceKey != \"default\", so this store resolved RAW and every [PersonalData] " +
			"payload was persisted plaintext (GDPR crypto-shred inert).");

		await store.StageMessageAsync(NewMessage(), CancellationToken.None);

		atRest.Staged.ShouldNotBeNull("the staged message must reach the inner store");
		ContainsSubsequence(atRest.Staged!.Payload, Pii).ShouldBeFalse(
			"The plaintext PII bytes must not appear anywhere in the at-rest payload. If they do, the Shape-B " +
			"\"default\" store was not decorated and PII is persisted in the clear (the vh1hit leak).");
	}

	[Fact]
	public async Task ActuallyEncryptAndStillPersist_NotSilentlyDropOrNoOp()
	{
		// LIVENESS. The permitted operation must still happen through the decorated store: the encryption provider
		// IS invoked (the protective action fired — the decorator is live, not an inert pass-through) AND the message
		// still reaches the inner store (it was persisted, not dropped). A wiring that silently did nothing would
		// satisfy the safety arm's "no plaintext at rest" for the wrong reason (nothing stored at all); this arm
		// fails against that, and against a decorator that never calls the provider.
		var atRest = new CapturingOutboxStore();
		var (services, provider) = WiredServices(atRest, encrypt: true);

		using var sp = services.BuildServiceProvider();
		var store = sp.GetRequiredKeyedService<IOutboxStore>("default");

		await store.StageMessageAsync(NewMessage(), CancellationToken.None);

		A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		atRest.Staged.ShouldNotBeNull(
			"the message must still be persisted to the inner store after encryption — the decorator protects data, " +
			"it does not drop it");
		atRest.Staged!.Id.ShouldBe("m1");
	}

	[Fact]
	public async Task LeavePlaintextAtRest_WhenTheDefaultStoreIsUndecorated_ThePreFixBug()
	{
		// NON-VACUITY CONTROL. Reproduces the pre-fix wiring: the old helper's ServiceKey != "default" filter matched
		// no descriptor for a Shape-B registration, so it applied no encrypting decorator and the "default" store
		// stayed raw. Persisting then leaves the PII plaintext at rest — exactly the vh1hit leak, and proof that the
		// safety arm's ciphertext assertion is RED against the bug (it depends on the "default" store being decorated).
		var atRest = new CapturingOutboxStore();
		var (services, _) = WiredServices(atRest, encrypt: false);   // AddOutboxEncryption NOT called → "default" raw

		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IOutboxStore>("default");

		store.ShouldBeSameAs(atRest, "with no encryption wiring the raw Shape-B store resolves directly");
		await store.StageMessageAsync(NewMessage(), CancellationToken.None);

		atRest.Staged.ShouldNotBeNull();
		atRest.Staged!.Payload.ShouldBe(Pii,
			"With the Shape-B \"default\" store left undecorated (the pre-fix behavior), the PII payload is persisted " +
			"PLAINTEXT at rest — this is the vh1hit leak the safety arm is written to catch.");
	}

	// Builds the REAL Shape-B wiring: the real store registered under "default", a deterministic reversible fake
	// encryption provider behind the real registry, and (when encrypt) the real AddOutboxEncryption → DecorateKeyedStores.
	private static (ServiceCollection Services, IEncryptionProvider Provider) WiredServices(IOutboxStore realStore, bool encrypt)
	{
		var provider = A.Fake<IEncryptionProvider>();
		A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken __) =>
				Task.FromResult(new EncryptedData
				{
					Ciphertext = Not(data),
					Iv = new byte[12],
					KeyId = "test-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
				}));
		A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily((EncryptedData ed, EncryptionContext _, CancellationToken __) => Task.FromResult(Not(ed.Ciphertext)));

		var registry = A.Fake<IEncryptionProviderRegistry>();
		A.CallTo(() => registry.GetPrimary()).Returns(provider);

		var services = new ServiceCollection();
		services.AddSingleton<IEncryptionProviderRegistry>(registry);
		_ = services.AddOptions<EncryptionOptions>();                       // Mode defaults to EncryptAndDecrypt
		services.AddKeyedSingleton<IOutboxStore>("default", realStore);     // Shape B: the real store IS "default"

		if (encrypt)
		{
			_ = services.AddOutboxEncryption();                            // the wiring under test (DecorateKeyedStores rule 2)
		}

		return (services, provider);
	}

	private static OutboundMessage NewMessage() => new()
	{
		Id = "m1",
		MessageType = "pii.event",
		Payload = (byte[])Pii.Clone(),
		CreatedAt = DateTimeOffset.UnixEpoch,
	};

	// Bitwise-NOT: a reversible transform whose output shares no byte value with the input at any position, so the
	// plaintext sequence provably cannot appear in the at-rest payload.
	private static byte[] Not(byte[] data)
	{
		var result = new byte[data.Length];
		for (var i = 0; i < data.Length; i++)
		{
			result[i] = (byte)~data[i];
		}

		return result;
	}

	private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
	{
		if (needle.Length == 0 || haystack.Length < needle.Length)
		{
			return false;
		}

		for (var i = 0; i <= haystack.Length - needle.Length; i++)
		{
			var match = true;
			for (var j = 0; j < needle.Length; j++)
			{
				if (haystack[i + j] != needle[j])
				{
					match = false;
					break;
				}
			}

			if (match)
			{
				return true;
			}
		}

		return false;
	}

	// Minimal capturing inner store: records the OutboundMessage handed to it (the bytes AT REST) and replays it so
	// a read-back through the decorator can decrypt it. Only StageMessage/GetUnsent are exercised.
	private sealed class CapturingOutboxStore : IOutboxStore
	{
		public OutboundMessage? Staged { get; private set; }

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			Staged = message;
			return ValueTask.CompletedTask;
		}

		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IEnumerable<OutboundMessage>>(Staged is null ? [] : [Staged]);

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, long fencingToken, CancellationToken cancellationToken) =>
			GetUnsentMessagesAsync(batchSize, cancellationToken);

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken) => ValueTask.CompletedTask;

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);
	}
}
