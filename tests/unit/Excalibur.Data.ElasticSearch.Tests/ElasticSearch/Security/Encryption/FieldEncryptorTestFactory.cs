// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Security.Encryption;

/// <summary>
/// Builds a <see cref="FieldEncryptor"/> wired to an in-memory <see cref="IKeyManagementProvider"/> and
/// <see cref="IEncryptionProviderRegistry"/> -- the direct-construction equivalent of what
/// <c>services.AddDevEncryption()</c> wires through DI, for unit tests that construct the encryptor by hand.
/// </summary>
internal static class FieldEncryptorTestFactory
{
	internal static FieldEncryptor Create(EncryptionOptions options)
	{
		var keyManagement = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var provider = new AesGcmEncryptionProvider(keyManagement, NullLogger<AesGcmEncryptionProvider>.Instance);
		var registry = new SingleProviderEncryptionRegistry(provider);
		return new FieldEncryptor(keyManagement, registry, Options.Create(options), NullLogger<FieldEncryptor>.Instance);
	}
}

/// <summary>
/// The minimal <see cref="IEncryptionProviderRegistry"/> a unit test needs: one provider, always primary.
/// Implements the interface directly rather than deriving from the shipped (internal)
/// <c>EncryptionProviderRegistry</c>, so the fixture exercises <see cref="FieldEncryptor"/> against the real
/// interface contract.
/// </summary>
internal sealed class SingleProviderEncryptionRegistry(IEncryptionProvider provider) : IEncryptionProviderRegistry
{
	public void Register(string providerId, IEncryptionProvider provider) =>
		throw new NotSupportedException("Test double carries exactly one provider.");

	public IEncryptionProvider? GetProvider(string providerId) => provider;

	public IEncryptionProvider GetPrimary() => provider;

	public IReadOnlyList<IEncryptionProvider> GetLegacyProviders() => [];

	public IEncryptionProvider? FindDecryptionProvider(EncryptedData encryptedData) => provider;

	public IReadOnlyList<IEncryptionProvider> GetAll() => [provider];

	public void SetPrimary(string providerId)
	{
		// Single-provider fixture: already primary, nothing to change.
	}
}
