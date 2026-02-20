// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Benchmarks;

/// <summary>
/// Benchmarks for encryption operations.
/// </summary>
/// <remarks>
/// Per AD-257-2, uses [MemoryDiagnoser] for allocation tracking.
/// These benchmarks establish baselines for bd-5jn9 (boxing) and bd-q7fj (string allocations).
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class EncryptionBenchmarks
{
	private IEncryptionProvider _provider = null!;
	private EncryptionContext _context = null!;
	private byte[] _smallPayload = null!;
	private byte[] _mediumPayload = null!;
	private byte[] _largePayload = null!;
	private EncryptedData _encryptedSmall = null!;
	private EncryptedData _encryptedMedium = null!;
	private EncryptedData _encryptedLarge = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Use AES-GCM provider with null loggers for benchmarking
		var keyProvider = new InMemoryKeyManagementProvider(
			NullLogger<InMemoryKeyManagementProvider>.Instance);
		_provider = new AesGcmEncryptionProvider(
			keyProvider,
			NullLogger<AesGcmEncryptionProvider>.Instance);
		_context = new EncryptionContext { TenantId = "benchmark-tenant", Purpose = "benchmark" };

		// Create payloads of different sizes
		_smallPayload = new byte[512]; // 512 bytes
		_mediumPayload = new byte[5 * 1024]; // 5 KB
		_largePayload = new byte[100 * 1024]; // 100 KB

		// Fill with random data
		Random.Shared.NextBytes(_smallPayload);
		Random.Shared.NextBytes(_mediumPayload);
		Random.Shared.NextBytes(_largePayload);

		// Pre-encrypt for decryption benchmarks
		_encryptedSmall = _provider.EncryptAsync(_smallPayload, _context, CancellationToken.None).GetAwaiter().GetResult();
		_encryptedMedium = _provider.EncryptAsync(_mediumPayload, _context, CancellationToken.None).GetAwaiter().GetResult();
		_encryptedLarge = _provider.EncryptAsync(_largePayload, _context, CancellationToken.None).GetAwaiter().GetResult();
	}

	#region Encryption Benchmarks

	[Benchmark(Baseline = true)]
	public async Task<EncryptedData> Encrypt_SmallPayload()
	{
		return await _provider.EncryptAsync(_smallPayload, _context, CancellationToken.None);
	}

	[Benchmark]
	public async Task<EncryptedData> Encrypt_MediumPayload()
	{
		return await _provider.EncryptAsync(_mediumPayload, _context, CancellationToken.None);
	}

	[Benchmark]
	public async Task<EncryptedData> Encrypt_LargePayload()
	{
		return await _provider.EncryptAsync(_largePayload, _context, CancellationToken.None);
	}

	#endregion Encryption Benchmarks

	#region Decryption Benchmarks

	[Benchmark]
	public async Task<byte[]> Decrypt_SmallPayload()
	{
		return await _provider.DecryptAsync(_encryptedSmall, _context, CancellationToken.None);
	}

	[Benchmark]
	public async Task<byte[]> Decrypt_MediumPayload()
	{
		return await _provider.DecryptAsync(_encryptedMedium, _context, CancellationToken.None);
	}

	[Benchmark]
	public async Task<byte[]> Decrypt_LargePayload()
	{
		return await _provider.DecryptAsync(_encryptedLarge, _context, CancellationToken.None);
	}

	#endregion Decryption Benchmarks
}
