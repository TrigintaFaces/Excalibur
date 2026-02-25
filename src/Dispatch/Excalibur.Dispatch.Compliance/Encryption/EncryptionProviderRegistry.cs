// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Thread-safe registry for managing multiple encryption providers with primary/legacy support.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides zero-downtime provider migration by maintaining a primary provider
/// for new encryptions and legacy providers for decrypting old data.
/// </para>
/// <para>
/// Thread safety is achieved using <see cref="ConcurrentDictionary{TKey,TValue}"/> for provider storage
/// and explicit locking for primary/legacy list modifications.
/// </para>
/// </remarks>
public sealed class EncryptionProviderRegistry : IEncryptionProviderRegistry
{
	private static readonly CompositeFormat ProviderAlreadyRegisteredFormat =
		CompositeFormat.Parse(Resources.EncryptionProviderRegistry_ProviderAlreadyRegistered);

	private static readonly CompositeFormat PrimaryProviderNotFoundFormat =
		CompositeFormat.Parse(Resources.EncryptionProviderRegistry_PrimaryProviderNotFound);

	private readonly ConcurrentDictionary<string, IEncryptionProvider> _providers =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly List<string> _legacyProviderIds = [];
#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	private string? _primaryProviderId;

	/// <inheritdoc/>
	public void Register(string providerId, IEncryptionProvider provider)
	{
		ArgumentNullException.ThrowIfNull(providerId);
		ArgumentNullException.ThrowIfNull(provider);

		if (!_providers.TryAdd(providerId, provider))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					ProviderAlreadyRegisteredFormat,
					providerId));
		}
	}

	/// <inheritdoc/>
	public IEncryptionProvider? GetProvider(string providerId)
	{
		ArgumentNullException.ThrowIfNull(providerId);
		return _providers.TryGetValue(providerId, out var provider) ? provider : null;
	}

	/// <inheritdoc/>
	public IEncryptionProvider GetPrimary()
	{
		if (_primaryProviderId is null)
		{
			throw new InvalidOperationException(
				Resources.EncryptionProviderRegistry_NoPrimaryProviderConfigured);
		}

		if (!_providers.TryGetValue(_primaryProviderId, out var provider))
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					PrimaryProviderNotFoundFormat,
					_primaryProviderId));
		}

		return provider;
	}

	/// <inheritdoc/>
	public IReadOnlyList<IEncryptionProvider> GetLegacyProviders()
	{
		lock (_lock)
		{
			// Return a copy to prevent external modification
			// Filter out any providers that may have been removed
			return _legacyProviderIds
				.Select(id => _providers.TryGetValue(id, out var p) ? p : null)
				.Where(p => p is not null)
				.Cast<IEncryptionProvider>()
				.ToList();
		}
	}

	/// <inheritdoc/>
	public IEncryptionProvider? FindDecryptionProvider(EncryptedData encryptedData)
	{
		ArgumentNullException.ThrowIfNull(encryptedData);

		// Fast path: Try primary provider first (covers 99% of cases in a stable system)
		if (_primaryProviderId is not null &&
			_providers.TryGetValue(_primaryProviderId, out var primaryProvider) &&
			CanDecrypt(primaryProvider, encryptedData))
		{
			return primaryProvider;
		}

		// Slow path: Try legacy providers in registration order (most recent first)
		lock (_lock)
		{
			foreach (var legacyId in _legacyProviderIds)
			{
				if (_providers.TryGetValue(legacyId, out var legacyProvider) &&
					CanDecrypt(legacyProvider, encryptedData))
				{
					return legacyProvider;
				}
			}
		}

		// Last resort: Try all registered providers
		// This handles edge cases where a provider wasn't registered as primary or legacy
		foreach (var kvp in _providers)
		{
			// Skip primary and legacy (already checked)
			if (kvp.Key.Equals(_primaryProviderId, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			bool isLegacy;
			lock (_lock)
			{
				isLegacy = _legacyProviderIds.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase);
			}

			if (isLegacy)
			{
				continue;
			}

			if (CanDecrypt(kvp.Value, encryptedData))
			{
				return kvp.Value;
			}
		}

		return null;
	}

	/// <inheritdoc/>
	public IReadOnlyList<IEncryptionProvider> GetAll()
	{
		return _providers.Values.ToList();
	}

	/// <inheritdoc/>
	public void SetPrimary(string providerId)
	{
		ArgumentNullException.ThrowIfNull(providerId);

		if (!_providers.ContainsKey(providerId))
		{
			throw new InvalidOperationException(
				$"Provider '{providerId}' is not registered. " +
				"Register the provider first using Register(providerId, provider).");
		}

		lock (_lock)
		{
			_primaryProviderId = providerId;

			// Remove from legacy list if present (a provider can't be both primary and legacy)
			_ = _legacyProviderIds.RemoveAll(id =>
				string.Equals(id, providerId, StringComparison.OrdinalIgnoreCase));
		}
	}

	/// <summary>
	/// Adds a provider to the legacy providers list.
	/// </summary>
	/// <param name="providerId">The provider ID to add as legacy.</param>
	/// <remarks>
	/// <para>
	/// This is an internal method used by the configuration builder to set up legacy providers.
	/// Legacy providers are used during migration to decrypt data encrypted with previous providers.
	/// </para>
	/// <para>
	/// The provider must be registered first. If the provider is currently primary, it will
	/// remain as both primary and legacy (common during migration transition).
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown if the provider ID is not registered.</exception>
	internal void AddLegacyProvider(string providerId)
	{
		ArgumentNullException.ThrowIfNull(providerId);

		if (!_providers.ContainsKey(providerId))
		{
			throw new InvalidOperationException(
				$"Provider '{providerId}' is not registered. " +
				"Register the provider first using Register(providerId, provider).");
		}

		lock (_lock)
		{
			if (!_legacyProviderIds.Contains(providerId, StringComparer.OrdinalIgnoreCase))
			{
				// Add to front of list (most recently added legacy has highest priority)
				_legacyProviderIds.Insert(0, providerId);
			}
		}
	}

	/// <summary>
	/// Determines whether a provider can decrypt the given encrypted data.
	/// </summary>
	/// <param name="provider">The provider to check.</param>
	/// <param name="encryptedData">The encrypted data to decrypt.</param>
	/// <returns><c>true</c> if the provider supports the algorithm; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// <para>
	/// This method checks algorithm support based on the provider type. Currently we support:
	/// <list type="bullet">
	/// <item><see cref="AesGcmEncryptionProvider"/> for <see cref="EncryptionAlgorithm.Aes256Gcm"/></item>
	/// <item><see cref="RotatingEncryptionProvider"/> (delegates to wrapped provider)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Actual key availability is verified during the decrypt operation, not here.
	/// This method only checks if the provider supports the algorithm in principle.
	/// </para>
	/// </remarks>
	private static bool CanDecrypt(IEncryptionProvider provider, EncryptedData encryptedData)
	{
		// Check if the provider supports the algorithm based on its type
		// This is a heuristic - providers should ideally expose their supported algorithms
		return encryptedData.Algorithm switch
		{
			EncryptionAlgorithm.Aes256Gcm =>
				provider is AesGcmEncryptionProvider or RotatingEncryptionProvider,

			EncryptionAlgorithm.Aes256CbcHmac =>
				// Legacy CBC mode - not recommended, may be supported by some providers
				false,

			_ => false
		};
	}
}
