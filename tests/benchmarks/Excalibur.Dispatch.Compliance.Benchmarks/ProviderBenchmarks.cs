// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Benchmarks;

/// <summary>
/// Benchmarks for IEncryptionProviderRegistry operations.
/// </summary>
/// <remarks>
/// Per AD-257-2, uses [MemoryDiagnoser] for allocation tracking.
/// These benchmarks help identify boxing and allocation issues in provider lookup.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ProviderBenchmarks
{
	private IEncryptionProviderRegistry _registry = null!;
	private EncryptedData _encryptedData = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Create registry with multiple providers
		var keyProvider = new InMemoryKeyManagementProvider(
			NullLogger<InMemoryKeyManagementProvider>.Instance);
		var provider = new AesGcmEncryptionProvider(
			keyProvider,
			NullLogger<AesGcmEncryptionProvider>.Instance);

		_registry = new EncryptionProviderRegistry();
		_registry.Register("primary", provider);
		_registry.SetPrimary("primary");

		// Create sample encrypted data
		_encryptedData = new EncryptedData
		{
			Ciphertext = new byte[32],
			KeyId = "primary-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = new byte[12],
			EncryptedAt = DateTimeOffset.UtcNow
		};
	}

	[Benchmark(Baseline = true)]
	public IEncryptionProvider GetPrimary()
	{
		return _registry.GetPrimary();
	}

	[Benchmark]
	public IEncryptionProvider? GetProvider_ById()
	{
		return _registry.GetProvider("primary");
	}

	[Benchmark]
	public IEncryptionProvider? FindDecryptionProvider()
	{
		return _registry.FindDecryptionProvider(_encryptedData);
	}
}
