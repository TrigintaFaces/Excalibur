// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for the encryption provider registry.
/// </summary>
public sealed class EncryptionProviderRegistryOptions
{
	/// <summary>
	/// Gets or sets the identifier of the primary encryption provider.
	/// </summary>
	/// <remarks>
	/// The primary provider is used for all new encryption operations.
	/// This can be changed at runtime via <see cref="Encryption.IEncryptionProviderRegistry.SetPrimary"/>.
	/// </remarks>
	public string? PrimaryProviderId { get; set; }

	/// <summary>
	/// Gets or sets the identifiers of legacy encryption providers.
	/// </summary>
	/// <remarks>
	/// Legacy providers are used during migration to decrypt data encrypted with
	/// previous providers. They are not used for new encryption operations.
	/// </remarks>
	public List<string> LegacyProviderIds { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to throw when no provider can decrypt data.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, <see cref="Encryption.IEncryptionProviderRegistry.FindDecryptionProvider"/>
	/// will throw if no provider can handle the encrypted data.
	/// When <c>false</c> (default), it returns <c>null</c>.
	/// </remarks>
	public bool ThrowOnDecryptionProviderNotFound { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to validate provider configuration on startup.
	/// </summary>
	/// <remarks>
	/// When <c>true</c> (default), the registry validates that at least one provider is
	/// registered and a primary is set during application startup.
	/// </remarks>
	public bool ValidateOnStartup { get; set; } = true;
}
