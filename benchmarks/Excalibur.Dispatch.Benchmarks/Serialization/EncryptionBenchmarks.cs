// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Benchmarks for AES-256-GCM encryption performance.
/// Measures encryption and decryption throughput across different payload sizes.
/// </summary>
/// <remarks>
/// <para>
/// Performance Targets (per ADR-051):
/// </para>
/// <list type="bullet">
///   <item>Small payload (1KB): &lt; 50μs encrypt, &lt; 50μs decrypt</item>
///   <item>Medium payload (10KB): &lt; 200μs encrypt, &lt; 200μs decrypt</item>
///   <item>Large payload (100KB): &lt; 1ms encrypt, &lt; 1ms decrypt</item>
///   <item>Throughput: &gt; 100 MB/s for bulk encryption</item>
/// </list>
/// <para>
/// These benchmarks measure raw cryptographic performance using .NET's AesGcm class
/// directly, isolated from key management overhead.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
#pragma warning disable CA1001 // BenchmarkDotNet manages lifecycle via [GlobalCleanup]
public class EncryptionBenchmarks
#pragma warning restore CA1001
{
	private const int KeySizeBytes = 32; // AES-256
	private const int NonceSizeBytes = 12; // GCM standard nonce
	private const int TagSizeBytes = 16; // 128-bit auth tag

	private AesGcm _aesGcm = null!;
	private byte[] _key = null!;
	private byte[] _nonce = null!;
	private byte[] _smallPayload = null!;
	private byte[] _mediumPayload = null!;
	private byte[] _largePayload = null!;
	private byte[] _veryLargePayload = null!;
	private byte[] _smallCiphertext = null!;
	private byte[] _mediumCiphertext = null!;
	private byte[] _largeCiphertext = null!;
	private byte[] _veryLargeCiphertext = null!;
	private byte[] _tag = null!;

	/// <summary>
	/// Initialize encryption provider and test payloads.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create key and nonce
		_key = new byte[KeySizeBytes];
		_nonce = new byte[NonceSizeBytes];
		_tag = new byte[TagSizeBytes];
		RandomNumberGenerator.Fill(_key);
		RandomNumberGenerator.Fill(_nonce);

		// Create AES-GCM instance
		_aesGcm = new AesGcm(_key, TagSizeBytes);

		// Create test payloads
		_smallPayload = new byte[1024]; // 1KB
		_mediumPayload = new byte[10 * 1024]; // 10KB
		_largePayload = new byte[100 * 1024]; // 100KB
		_veryLargePayload = new byte[1024 * 1024]; // 1MB

		RandomNumberGenerator.Fill(_smallPayload);
		RandomNumberGenerator.Fill(_mediumPayload);
		RandomNumberGenerator.Fill(_largePayload);
		RandomNumberGenerator.Fill(_veryLargePayload);

		// Pre-allocate ciphertext buffers
		_smallCiphertext = new byte[_smallPayload.Length];
		_mediumCiphertext = new byte[_mediumPayload.Length];
		_largeCiphertext = new byte[_largePayload.Length];
		_veryLargeCiphertext = new byte[_veryLargePayload.Length];

		// Pre-encrypt for decryption benchmarks
		_aesGcm.Encrypt(_nonce, _smallPayload, _smallCiphertext, _tag);
		_aesGcm.Encrypt(_nonce, _mediumPayload, _mediumCiphertext, _tag);
		_aesGcm.Encrypt(_nonce, _largePayload, _largeCiphertext, _tag);
		_aesGcm.Encrypt(_nonce, _veryLargePayload, _veryLargeCiphertext, _tag);
	}

	/// <summary>
	/// Cleanup resources.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_aesGcm?.Dispose();
		if (_key != null)
		{
			CryptographicOperations.ZeroMemory(_key);
		}
	}

	/// <summary>
	/// Benchmark: Encrypt small payload (1KB).
	/// Target: &lt; 50μs.
	/// </summary>
	[Benchmark(Baseline = true)]
	public void EncryptSmall()
	{
		var ciphertext = new byte[_smallPayload.Length];
		var tag = new byte[TagSizeBytes];
		_aesGcm.Encrypt(_nonce, _smallPayload, ciphertext, tag);
	}

	/// <summary>
	/// Benchmark: Decrypt small payload (1KB).
	/// Target: &lt; 50μs.
	/// </summary>
	[Benchmark]
	public void DecryptSmall()
	{
		var plaintext = new byte[_smallCiphertext.Length];
		_aesGcm.Decrypt(_nonce, _smallCiphertext, _tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Encrypt medium payload (10KB).
	/// Target: &lt; 200μs.
	/// </summary>
	[Benchmark]
	public void EncryptMedium()
	{
		var ciphertext = new byte[_mediumPayload.Length];
		var tag = new byte[TagSizeBytes];
		_aesGcm.Encrypt(_nonce, _mediumPayload, ciphertext, tag);
	}

	/// <summary>
	/// Benchmark: Decrypt medium payload (10KB).
	/// Target: &lt; 200μs.
	/// </summary>
	[Benchmark]
	public void DecryptMedium()
	{
		var plaintext = new byte[_mediumCiphertext.Length];
		_aesGcm.Decrypt(_nonce, _mediumCiphertext, _tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Encrypt large payload (100KB).
	/// Target: &lt; 1ms.
	/// </summary>
	[Benchmark]
	public void EncryptLarge()
	{
		var ciphertext = new byte[_largePayload.Length];
		var tag = new byte[TagSizeBytes];
		_aesGcm.Encrypt(_nonce, _largePayload, ciphertext, tag);
	}

	/// <summary>
	/// Benchmark: Decrypt large payload (100KB).
	/// Target: &lt; 1ms.
	/// </summary>
	[Benchmark]
	public void DecryptLarge()
	{
		var plaintext = new byte[_largeCiphertext.Length];
		_aesGcm.Decrypt(_nonce, _largeCiphertext, _tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Encrypt very large payload (1MB) for throughput measurement.
	/// Target: &gt; 100 MB/s throughput.
	/// </summary>
	[Benchmark]
	public void EncryptVeryLarge()
	{
		var ciphertext = new byte[_veryLargePayload.Length];
		var tag = new byte[TagSizeBytes];
		_aesGcm.Encrypt(_nonce, _veryLargePayload, ciphertext, tag);
	}

	/// <summary>
	/// Benchmark: Decrypt very large payload (1MB) for throughput measurement.
	/// Target: &gt; 100 MB/s throughput.
	/// </summary>
	[Benchmark]
	public void DecryptVeryLarge()
	{
		var plaintext = new byte[_veryLargeCiphertext.Length];
		_aesGcm.Decrypt(_nonce, _veryLargeCiphertext, _tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Round-trip (encrypt + decrypt) small payload.
	/// </summary>
	[Benchmark]
	public void RoundTripSmall()
	{
		var ciphertext = new byte[_smallPayload.Length];
		var tag = new byte[TagSizeBytes];
		var plaintext = new byte[_smallPayload.Length];

		_aesGcm.Encrypt(_nonce, _smallPayload, ciphertext, tag);
		_aesGcm.Decrypt(_nonce, ciphertext, tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Round-trip (encrypt + decrypt) medium payload.
	/// </summary>
	[Benchmark]
	public void RoundTripMedium()
	{
		var ciphertext = new byte[_mediumPayload.Length];
		var tag = new byte[TagSizeBytes];
		var plaintext = new byte[_mediumPayload.Length];

		_aesGcm.Encrypt(_nonce, _mediumPayload, ciphertext, tag);
		_aesGcm.Decrypt(_nonce, ciphertext, tag, plaintext);
	}

	/// <summary>
	/// Benchmark: Encrypt with fresh nonce generation (realistic scenario).
	/// </summary>
	[Benchmark]
	public void EncryptWithNonceGeneration()
	{
		var nonce = new byte[NonceSizeBytes];
		RandomNumberGenerator.Fill(nonce);

		var ciphertext = new byte[_smallPayload.Length];
		var tag = new byte[TagSizeBytes];
		_aesGcm.Encrypt(nonce, _smallPayload, ciphertext, tag);
	}
}
