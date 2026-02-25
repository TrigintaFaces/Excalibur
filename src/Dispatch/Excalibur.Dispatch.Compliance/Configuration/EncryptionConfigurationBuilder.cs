// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Marker interface for primary provider setting.
/// </summary>
internal interface IEncryptionProviderPrimarySetter
{
	string ProviderId { get; }
}

/// <summary>
/// Marker interface for legacy provider marking.
/// </summary>
internal interface IEncryptionProviderLegacyMarker
{
	string ProviderId { get; }
}

/// <summary>
/// Marker interface for provider initialization.
/// </summary>
internal interface IEncryptionProviderInitializer
{
}

/// <summary>
/// Fluent builder for configuring encryption providers.
/// </summary>
/// <remarks>
/// Use this builder to register and configure encryption providers for the registry.
/// All methods return the builder instance for fluent chaining.
/// </remarks>
public sealed class EncryptionConfigurationBuilder
{
	private static readonly CompositeFormat ProviderNotRegisteredFormat =
		CompositeFormat.Parse(Resources.EncryptionConfigurationBuilder_ProviderNotRegistered);

	private readonly IServiceCollection _services;
	private readonly List<string> _registeredProviderIds = [];
	private string? _primaryProviderId;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptionConfigurationBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	internal EncryptionConfigurationBuilder(IServiceCollection services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <summary>
	/// Registers an AES-256-GCM encryption provider with in-memory key management.
	/// </summary>
	/// <param name="providerId">Unique identifier for this provider. Auto-generated if null.</param>
	/// <param name="configureKeyManagement">Optional configuration for key management options.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// This provider is suitable for development and testing. For production,
	/// use <see cref="UseKeyManagement{TProvider}"/> with a cloud KMS provider.
	/// </remarks>
	public EncryptionConfigurationBuilder UseInMemoryKeyManagement(
		string? providerId = null,
		Action<InMemoryKeyManagementOptions>? configureKeyManagement = null)
	{
		providerId ??= $"inmemory-{Guid.NewGuid():N}";

		var keyManagementOptions = new InMemoryKeyManagementOptions();
		configureKeyManagement?.Invoke(keyManagementOptions);

		// Register key management provider
		_services.TryAddSingleton(keyManagementOptions);
		_services.TryAddSingleton<InMemoryKeyManagementProvider>();
		_services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<InMemoryKeyManagementProvider>());

		// Register the AES-GCM encryption provider
		_ = _services.AddSingleton<IEncryptionProvider>(sp =>
		{
			var keyMgmt = sp.GetRequiredService<IKeyManagementProvider>();
			var logger = sp.GetRequiredService<ILogger<AesGcmEncryptionProvider>>();
			var provider = new AesGcmEncryptionProvider(keyMgmt, logger);

			// Register with the registry
			var registry = sp.GetRequiredService<IEncryptionProviderRegistry>();
			registry.Register(providerId, provider);

			return provider;
		});

		_registeredProviderIds.Add(providerId);

		// Set as primary if this is the first provider
		if (_registeredProviderIds.Count == 1)
		{
			_primaryProviderId = providerId;
		}

		return this;
	}

	/// <summary>
	/// Registers a custom encryption provider type.
	/// </summary>
	/// <typeparam name="TProvider">The encryption provider type to register.</typeparam>
	/// <param name="providerId">Unique identifier for this provider. Auto-generated if null.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// The provider type must implement <see cref="IEncryptionProvider"/> and have a
	/// constructor that can be resolved by the DI container.
	/// </remarks>
	public EncryptionConfigurationBuilder UseKeyManagement<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TProvider>(string? providerId = null)
		where TProvider : class, IEncryptionProvider
	{
		providerId ??= $"{typeof(TProvider).Name}-{Guid.NewGuid():N}";

		// Register the provider type
		_services.TryAddSingleton<TProvider>();

		// Register it in the registry on resolution
		_ = _services.AddSingleton<IEncryptionProvider>(sp =>
		{
			var provider = sp.GetRequiredService<TProvider>();
			var registry = sp.GetRequiredService<IEncryptionProviderRegistry>();
			registry.Register(providerId, provider);
			return provider;
		});

		_registeredProviderIds.Add(providerId);

		// Set as primary if this is the first provider
		if (_registeredProviderIds.Count == 1)
		{
			_primaryProviderId = providerId;
		}

		return this;
	}

	/// <summary>
	/// Registers a provider instance directly.
	/// </summary>
	/// <param name="providerId">Unique identifier for this provider.</param>
	/// <param name="provider">The provider instance.</param>
	/// <returns>The builder for chaining.</returns>
	public EncryptionConfigurationBuilder UseProvider(string providerId, IEncryptionProvider provider)
	{
		ArgumentNullException.ThrowIfNull(providerId);
		ArgumentNullException.ThrowIfNull(provider);

		_ = _services.AddSingleton(sp =>
		{
			var registry = sp.GetRequiredService<IEncryptionProviderRegistry>();
			registry.Register(providerId, provider);
			return provider;
		});

		_registeredProviderIds.Add(providerId);

		// Set as primary if this is the first provider
		if (_registeredProviderIds.Count == 1)
		{
			_primaryProviderId = providerId;
		}

		return this;
	}

	/// <summary>
	/// Sets a registered provider as the primary provider for new encryption operations.
	/// </summary>
	/// <param name="providerId">The provider ID to set as primary.</param>
	/// <returns>The builder for chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the provider ID was not registered in this builder.
	/// </exception>
	public EncryptionConfigurationBuilder SetAsPrimary(string providerId)
	{
		ArgumentNullException.ThrowIfNull(providerId);

		if (!_registeredProviderIds.Contains(providerId, StringComparer.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"Provider '{providerId}' was not registered in this builder. " +
				"Register the provider first using UseInMemoryKeyManagement(), UseKeyManagement<T>(), or UseProvider().");
		}

		_primaryProviderId = providerId;

		// Register the primary setting to be applied after all providers are registered
		_ = _services.AddSingleton<IEncryptionProviderPrimarySetter>(sp =>
			new EncryptionProviderPrimarySetter(providerId));

		return this;
	}

	/// <summary>
	/// Marks a registered provider as a legacy provider for decryption only.
	/// </summary>
	/// <param name="providerId">The provider ID to mark as legacy.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// Legacy providers are used during migration to decrypt data encrypted with
	/// previous providers. They are not used for new encryption operations.
	/// </remarks>
	public EncryptionConfigurationBuilder AddLegacy(string providerId)
	{
		ArgumentNullException.ThrowIfNull(providerId);

		if (!_registeredProviderIds.Contains(providerId, StringComparer.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				string.Format(CultureInfo.InvariantCulture,
					ProviderNotRegisteredFormat,
					providerId));
		}

		// Register the legacy setting
		_ = _services.AddSingleton<IEncryptionProviderLegacyMarker>(sp =>
			new EncryptionProviderLegacyMarker(providerId));

		return this;
	}

	/// <summary>
	/// Configures encryption options.
	/// </summary>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The builder for chaining.</returns>
	public EncryptionConfigurationBuilder ConfigureOptions(Action<EncryptionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var options = new EncryptionOptions();
		configure(options);
		_ = _services.AddSingleton(options);

		return this;
	}

	/// <summary>
	/// Validates the builder configuration.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown if no providers were registered or no primary was set.
	/// </exception>
	internal void Validate()
	{
		if (_registeredProviderIds.Count == 0)
		{
			throw new InvalidOperationException(
				Resources.EncryptionConfigurationBuilder_NoProvidersRegistered);
		}

		// Register the primary provider setup
		if (_primaryProviderId is not null)
		{
			_ = _services.AddSingleton<IEncryptionProviderInitializer>(sp =>
			{
				var registry = sp.GetRequiredService<IEncryptionProviderRegistry>();
				registry.SetPrimary(_primaryProviderId);

				// Apply legacy markers
				foreach (var legacyMarker in sp.GetServices<IEncryptionProviderLegacyMarker>())
				{
					if (registry is EncryptionProviderRegistry concreteRegistry)
					{
						concreteRegistry.AddLegacyProvider(legacyMarker.ProviderId);
					}
				}

				return new EncryptionProviderInitializer();
			});
		}
	}
}

internal sealed class EncryptionProviderPrimarySetter(string providerId) : IEncryptionProviderPrimarySetter
{
	public string ProviderId { get; } = providerId;
}

internal sealed class EncryptionProviderLegacyMarker(string providerId) : IEncryptionProviderLegacyMarker
{
	public string ProviderId { get; } = providerId;
}

internal sealed class EncryptionProviderInitializer : IEncryptionProviderInitializer
{
}
