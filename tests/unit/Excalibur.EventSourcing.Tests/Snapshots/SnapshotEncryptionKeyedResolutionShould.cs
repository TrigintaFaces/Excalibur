// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Snapshots.Security;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// nce90r — author≠impl P0 security lock (crypto-shred keyed-bypass, the S886 <c>rw2ull</c> / S887
/// <c>hzul4z</c> class): a <b>keyed</b> snapshot-store registration decorated by
/// <c>AddSnapshotEncryption()</c> must, when resolved through the SAME keyed path a GDPR/repository consumer
/// uses (<c>GetRequiredKeyedService&lt;ISnapshotStore&gt;("default")</c>), be the ENCRYPTING chain — so a
/// <c>[PersonalData]</c> field is encrypted at rest. The fix (<c>DecorateSnapshotStore</c> re-registers
/// <b>keyed-if-keyed</b>) closes the bypass where the decorator was re-registered non-keyed, leaving the
/// keyed resolution pointing at the bare store → plaintext PII at rest.
/// </summary>
/// <remarks>
/// Real-DI (S873): resolves through a real <see cref="ServiceProvider"/> built by the production
/// registration (<c>AddSnapshotEncryption</c>) and asserts the EMITTED behaviour — the inner store receives
/// ENCRYPTED bytes on the keyed path — not merely that a descriptor exists.
/// <para>
/// SAFETY — on the keyed <c>"default"</c> path the persisted <c>Data</c> is encrypted (the plaintext PII
/// never reaches the inner store). <b>RED on the pre-fix</b>: <c>DecorateSnapshotStore</c> re-registered the
/// decorator NON-keyed, so <c>GetRequiredKeyedService("default")</c> resolves the bare inner store (or
/// nothing) → the inner receives PLAINTEXT → this assertion fails.
/// LIVENESS — the encrypting chain still round-trips: a load through the same keyed store returns the
/// original plaintext (decryption is wired), so encryption is not "safe by breaking reads".
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SnapshotEncryptionKeyedResolutionShould
{
	private const string DefaultKey = "default";
	private static readonly byte[] PlaintextPii = "personal-data:SSN=123-45-6789"u8.ToArray();

	[Fact]
	public async Task EncryptPersistedDataWhenResolvedThroughTheKeyedDefaultPath()
	{
		// Arrange — a KEYED-"default" inner store (the shape GDPR/repository resolves), then the production
		// encryption decoration. The inner is registered before AddSnapshotEncryption so the decorator wraps it.
		var inner = new RecordingSnapshotStore();
		var services = new ServiceCollection();
		_ = services.AddSingleton<ISnapshotEncryptor>(new XorSnapshotEncryptor());
		_ = services.AddKeyedSingleton<ISnapshotStore>(DefaultKey, inner);
		_ = services.AddSnapshotEncryption();

		await using var provider = services.BuildServiceProvider();

		// Act — resolve through the SAME keyed path the consumer uses, and save a snapshot carrying PII.
		var store = provider.GetRequiredKeyedService<ISnapshotStore>(DefaultKey);
		await store.SaveSnapshotAsync(
			new TestSnapshot("agg-1", "Customer", PlaintextPii), CancellationToken.None).ConfigureAwait(false);

		// Assert SAFETY — the inner store received ENCRYPTED bytes, never the plaintext PII. If the keyed
		// resolution bypassed the encrypting decorator (the pre-fix bug), the inner would hold plaintext here.
		inner.LastSavedData.ShouldNotBeNull("the keyed store must have persisted a snapshot");
		inner.LastSavedData.ShouldNotBe(PlaintextPii,
			"the keyed-default resolution must go through the encrypting decorator — plaintext PII at rest is the "
			+ "crypto-shred keyed-bypass this lock guards against");
		inner.LastSavedData.ShouldBe(XorSnapshotEncryptor.Transform(PlaintextPii),
			"the persisted bytes must be exactly the encryptor's output (the decorator is in the resolved keyed chain)");
	}

	[Fact]
	public async Task RoundTripThePlaintextThroughTheKeyedDefaultPath()
	{
		// LIVENESS — the encrypting chain still reads back the original plaintext (decrypt is wired), so the
		// safety property is not achieved by breaking reads.
		var inner = new RecordingSnapshotStore();
		var services = new ServiceCollection();
		_ = services.AddSingleton<ISnapshotEncryptor>(new XorSnapshotEncryptor());
		_ = services.AddKeyedSingleton<ISnapshotStore>(DefaultKey, inner);
		_ = services.AddSnapshotEncryption();

		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<ISnapshotStore>(DefaultKey);

		await store.SaveSnapshotAsync(
			new TestSnapshot("agg-2", "Customer", PlaintextPii), CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync("agg-2", "Customer", CancellationToken.None).ConfigureAwait(false);

		loaded.ShouldNotBeNull("the keyed encrypting store must return the stored snapshot");
		loaded!.Data.ToArray().ShouldBe(PlaintextPii,
			"the encrypting chain must decrypt on read — the consumer sees the original plaintext, encryption is at rest only");
	}

	// Deterministic, reversible test encryptor: ciphertext != plaintext (so "encrypted at rest" is observable),
	// and Decrypt reverses Encrypt (so the round-trip liveness arm holds).
	private sealed class XorSnapshotEncryptor : ISnapshotEncryptor
	{
		internal static byte[] Transform(byte[] data)
		{
			var result = new byte[data.Length];
			for (var i = 0; i < data.Length; i++)
			{
				result[i] = (byte)(data[i] ^ 0xA5);
			}

			return result;
		}

		public ValueTask<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken) =>
			new(Transform(data));

		public ValueTask<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken) =>
			new(Transform(data));
	}

	// Records exactly what bytes reach the store. Implements ISnapshotStore DIRECTLY (no first-party base that
	// could supply the member) so the interface contract binds directly.
	private sealed class RecordingSnapshotStore : ISnapshotStore
	{
		private ISnapshot? _saved;

		public byte[]? LastSavedData { get; private set; }

		public ValueTask SaveSnapshotAsync(ISnapshot snapshot, CancellationToken cancellationToken)
		{
			_saved = snapshot;
			LastSavedData = snapshot.Data.ToArray();
			return ValueTask.CompletedTask;
		}

		public ValueTask<ISnapshot?> GetLatestSnapshotAsync(string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			new(_saved);

		public ValueTask DeleteSnapshotsAsync(string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;

		public ValueTask DeleteSnapshotsOlderThanAsync(string aggregateId, string aggregateType, long olderThanVersion, CancellationToken cancellationToken) =>
			ValueTask.CompletedTask;
	}

	private sealed class TestSnapshot(string aggregateId, string aggregateType, byte[] data) : ISnapshot
	{
		public string SnapshotId { get; } = Guid.NewGuid().ToString("N");

		// Single-tenant fixture. Declared explicitly rather than inherited, so a reader can see
		// that this double is unscoped instead of assuming it.
		public string? TenantId { get; }

		public string AggregateId { get; } = aggregateId;

		public long Version { get; } = 1;

		public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UnixEpoch;

		public ReadOnlyMemory<byte> Data { get; } = data;

		public string AggregateType { get; } = aggregateType;

		public IDictionary<string, object>? Metadata => null;
	}
}
