// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

// Independent regression lock (author != implementer, TestsDeveloper) — the INBOX twin of the vh1hit P1 GDPR
// PII-at-rest leak. Symmetric to ShapeBOutboxEncryptionAtRestShould: `AddInbox<TStore>()` registers the real store
// directly under the keyed "default" service key (Shape B); the OLD store-encryption wiring filtered
// `ServiceKey != "default"`, so `AddInboxEncryption()` decorated nothing and [PersonalData] payloads were persisted
// PLAINTEXT at rest. The fix wires through the shared `DecorateKeyedStores<IInboxStore>` helper (rule 2 decorates
// the "default"-real store).
//
// Same fidelity/rationale as the outbox twin: REAL wiring (AddInboxEncryption → DecorateKeyedStores) + REAL
// EncryptingInboxStoreDecorator, asserting the payload handed to the store AT REST; deterministic reversible fake
// provider (the vh1hit defect is a WIRING bug). SAFETY (plaintext excluded at rest) is paired with LIVENESS
// (encryption fired + the entry is still created), and the control arm reproduces the pre-fix plaintext leak
// (non-vacuity).
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ShapeBInboxEncryptionAtRestShould
{
	private static readonly byte[] Pii = "tenant-a:jane.doe@example.com:SSN-123-45-6789"u8.ToArray();

	[Fact]
	public async Task EncryptTheShapeBDefaultStorePayloadAtRest_WhenAddInboxEncryptionIsWired()
	{
		// SAFETY + WIRING (the fix). Shape-B real store under "default" + AddInboxEncryption must wrap the "default"
		// store in the encrypting decorator and hand the inner store CIPHERTEXT — the plaintext PII must not be at
		// rest. RED against the pre-fix ServiceKey!="default" filter (see the control arm).
		var atRest = new CapturingInboxStore();
		var (services, _) = WiredServices(atRest, encrypt: true);

		using var sp = services.BuildServiceProvider();
		var store = sp.GetRequiredKeyedService<IInboxStore>("default");

		store.ShouldBeOfType<EncryptingInboxStoreDecorator>(
			"AddInboxEncryption must wrap the Shape-B \"default\" real store in the encrypting decorator. Before the " +
			"fix the wiring filtered ServiceKey != \"default\", so this store resolved RAW and every [PersonalData] " +
			"payload was persisted plaintext (GDPR crypto-shred inert).");

		_ = await store.CreateEntryAsync("m1", "h1", "pii.event", (byte[])Pii.Clone(), Meta(), CancellationToken.None);

		atRest.StagedPayload.ShouldNotBeNull("the entry must reach the inner store");
		ContainsSubsequence(atRest.StagedPayload!, Pii).ShouldBeFalse(
			"The plaintext PII bytes must not appear anywhere in the at-rest payload. If they do, the Shape-B " +
			"\"default\" inbox store was not decorated and PII is persisted in the clear (the vh1hit leak).");
	}

	[Fact]
	public async Task ActuallyEncryptAndStillCreateTheEntry_NotSilentlyDropOrNoOp()
	{
		// LIVENESS. The permitted operation still happens: the provider IS invoked (protective action fired, the
		// decorator is live) AND the entry still reaches the inner store (created, not dropped). A wiring that did
		// nothing would fail here.
		var atRest = new CapturingInboxStore();
		var (services, provider) = WiredServices(atRest, encrypt: true);

		using var sp = services.BuildServiceProvider();
		var store = sp.GetRequiredKeyedService<IInboxStore>("default");

		_ = await store.CreateEntryAsync("m1", "h1", "pii.event", (byte[])Pii.Clone(), Meta(), CancellationToken.None);

		A.CallTo(() => provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		atRest.StagedPayload.ShouldNotBeNull(
			"the entry must still be created on the inner store after encryption — the decorator protects data, it " +
			"does not drop it");
	}

	[Fact]
	public async Task LeavePlaintextAtRest_WhenTheDefaultStoreIsUndecorated_ThePreFixBug()
	{
		// NON-VACUITY CONTROL. Reproduces the pre-fix wiring: with the Shape-B "default" store left undecorated (the
		// ServiceKey != "default" filter matched nothing), the PII payload is persisted PLAINTEXT at rest — the
		// vh1hit leak, and proof the safety arm is RED against the bug.
		var atRest = new CapturingInboxStore();
		var (services, _) = WiredServices(atRest, encrypt: false);   // AddInboxEncryption NOT called → "default" raw

		using var sp = services.BuildServiceProvider();
		var store = sp.GetRequiredKeyedService<IInboxStore>("default");

		store.ShouldBeSameAs(atRest, "with no encryption wiring the raw Shape-B store resolves directly");
		_ = await store.CreateEntryAsync("m1", "h1", "pii.event", (byte[])Pii.Clone(), Meta(), CancellationToken.None);

		atRest.StagedPayload.ShouldNotBeNull();
		atRest.StagedPayload!.ShouldBe(Pii,
			"With the Shape-B \"default\" store left undecorated (the pre-fix behavior), the PII payload is persisted " +
			"PLAINTEXT at rest — this is the vh1hit leak the safety arm is written to catch.");
	}

	private static (ServiceCollection Services, IEncryptionProvider Provider) WiredServices(IInboxStore realStore, bool encrypt)
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

		var registry = A.Fake<IEncryptionProviderRegistry>();
		A.CallTo(() => registry.GetPrimary()).Returns(provider);

		var services = new ServiceCollection();
		services.AddSingleton<IEncryptionProviderRegistry>(registry);
		_ = services.AddOptions<EncryptionOptions>();                      // Mode defaults to EncryptAndDecrypt
		services.AddKeyedSingleton<IInboxStore>("default", realStore);     // Shape B: the real store IS "default"

		if (encrypt)
		{
			_ = services.AddInboxEncryption();                            // the wiring under test (DecorateKeyedStores rule 2)
		}

		return (services, provider);
	}

	private static Dictionary<string, object> Meta() => new(StringComparer.Ordinal);

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

	// Minimal capturing inner inbox store: records the payload handed to CreateEntryAsync (the bytes AT REST).
	private sealed class CapturingInboxStore : IInboxStore
	{
		public byte[]? StagedPayload { get; private set; }

		public ValueTask<InboxEntry> CreateEntryAsync(
			string messageId,
			string handlerType,
			string messageType,
			byte[] payload,
			IDictionary<string, object> metadata,
			CancellationToken cancellationToken)
		{
			StagedPayload = payload;
			return ValueTask.FromResult(new InboxEntry
			{
				MessageId = messageId,
				HandlerType = handlerType,
				MessageType = messageType,
				Payload = payload,
			});
		}

		public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;

		public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(true);

		public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult(false);

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
			ValueTask.FromResult<InboxEntry?>(null);

		public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;
	}
}
