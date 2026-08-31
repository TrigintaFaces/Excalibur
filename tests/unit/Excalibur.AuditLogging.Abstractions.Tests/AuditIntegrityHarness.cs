// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Builds the audit-integrity strategy the way a consumer does — through the public registration — with a
/// test-controlled key provider.
/// </summary>
/// <remarks>
/// The strategy type is <c>internal</c>, and it stays that way: reaching it through
/// <c>AddAuditIntegrity()</c> exercises the same composition a consumer gets, and needs no widening of
/// production visibility to do it. Registering the key provider first relies on the documented
/// <c>TryAddSingleton</c> override contract, so these tests also fail if that contract quietly changes.
/// </remarks>
internal static class AuditIntegrityHarness
{
	/// <summary>A 32-byte key — the minimum HMAC-SHA256 strength the options validator enforces.</summary>
	public static byte[] KeyA { get; } = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

	/// <summary>A second key of equal strength, for cross-key rejection and rotation arms.</summary>
	public static byte[] KeyB { get; } = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();

	public static IAuditIntegrityStrategy Strategy(IAuditSigningKeyProvider keyProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(keyProvider);
		_ = services.AddAuditIntegrity();

		return services.BuildServiceProvider().GetRequiredService<IAuditIntegrityStrategy>();
	}

	public static IAuditIntegrityStrategy StrategyWith(string keyId, byte[] key)
		=> Strategy(new StubKeyProvider(keyId, key));

	public static byte[] Content(string value) => AuditRecordCanonicalizer.Canonicalize(value);

	/// <summary>
	/// A key provider under the test's control: one current key, plus whatever historical keys the test
	/// chooses to make resolvable for verification.
	/// </summary>
	internal sealed class StubKeyProvider(string currentKeyId, byte[]? currentKey) : IAuditSigningKeyProvider
	{
		private readonly Dictionary<string, byte[]> _resolvable =
			currentKey is null ? [] : new Dictionary<string, byte[]>(StringComparer.Ordinal) { [currentKeyId] = currentKey };

		public bool ThrowOnCurrent { get; init; }

		public void AlsoResolve(string keyId, byte[] key) => _resolvable[keyId] = key;

		public void StopResolving(string keyId) => _resolvable.Remove(keyId);

		public ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken)
		{
			if (ThrowOnCurrent || currentKey is null)
			{
				throw new InvalidOperationException("No audit signing key is available.");
			}

			return ValueTask.FromResult((currentKeyId, currentKey));
		}

		public ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
			=> ValueTask.FromResult(_resolvable.TryGetValue(keyId, out var key) ? key : null);
	}

	/// <summary>A provider that hands back a key id the tag format cannot represent.</summary>
	internal sealed class MalformedKeyIdProvider(string keyId) : IAuditSigningKeyProvider
	{
		public ValueTask<(string KeyId, byte[] Key)> GetCurrentSigningKeyAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult((keyId, KeyA));

		public ValueTask<byte[]?> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
			=> ValueTask.FromResult<byte[]?>(KeyA);
	}
}
