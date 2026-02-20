// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Compliance.Benchmarks;

/// <summary>
/// Benchmarks for IsFieldEncrypted detection using magic bytes.
/// </summary>
/// <remarks>
/// Per AD-257-2, uses [MemoryDiagnoser] to track allocations.
/// Per AD-253-3, IsFieldEncrypted uses EXCR magic bytes (0x45 0x58 0x43 0x52).
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class DetectionBenchmarks
{
	private byte[] _encryptedData = null!;
	private byte[] _plaintextData = null!;
	private byte[] _shortData = null!;
	private byte[] _emptyData = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Encrypted data with EXCR magic bytes
		_encryptedData = new byte[1024];
		_encryptedData[0] = 0x45; // E
		_encryptedData[1] = 0x58; // X
		_encryptedData[2] = 0x43; // C
		_encryptedData[3] = 0x52; // R
		Random.Shared.NextBytes(_encryptedData.AsSpan(4));

		// Plaintext data (JSON-like)
		_plaintextData = System.Text.Encoding.UTF8.GetBytes(
			"{\"name\":\"test\",\"value\":\"some data here\",\"timestamp\":\"2025-01-01T00:00:00Z\"}");

		// Short data (less than magic bytes length)
		_shortData = [0x45, 0x58, 0x43];

		// Empty data
		_emptyData = [];
	}

	[Benchmark(Baseline = true)]
	public bool IsFieldEncrypted_DetectMagicBytes()
	{
		return EncryptedData.IsFieldEncrypted(_encryptedData.AsSpan());
	}

	[Benchmark]
	public bool IsFieldEncrypted_DetectPlaintext()
	{
		return EncryptedData.IsFieldEncrypted(_plaintextData.AsSpan());
	}

	[Benchmark]
	public bool IsFieldEncrypted_ShortData()
	{
		return EncryptedData.IsFieldEncrypted(_shortData.AsSpan());
	}

	[Benchmark]
	public bool IsFieldEncrypted_EmptyData()
	{
		return EncryptedData.IsFieldEncrypted(_emptyData.AsSpan());
	}

	[Benchmark]
	public bool IsFieldEncrypted_ByteArray_Encrypted()
	{
		return EncryptedData.IsFieldEncrypted(_encryptedData);
	}

	[Benchmark]
	public bool IsFieldEncrypted_ByteArray_Plaintext()
	{
		return EncryptedData.IsFieldEncrypted(_plaintextData);
	}

	[Benchmark]
	public bool IsFieldEncrypted_ByteArray_Null()
	{
		return EncryptedData.IsFieldEncrypted((byte[]?)null);
	}
}
