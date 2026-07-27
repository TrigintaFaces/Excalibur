// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>
/// Default <see cref="IComplianceEncryptionBuilder"/> implementation. Accumulates the encryption/key-management
/// selections, then materializes the DI registrations in <see cref="Build"/> so option-dependent service mappings
/// (for example the key-rotation wrapper replacing the base encryption provider) are applied deterministically.
/// </summary>
internal sealed class ComplianceEncryptionBuilder : IComplianceEncryptionBuilder
{
	private enum KeyManagementSelection
	{
		None,
		InMemory,
		Custom,
	}

	private KeyManagementSelection _keyManagement = KeyManagementSelection.None;
	private Action<IServiceCollection>? _registerCustomKeyManagement;
	private bool _keyRotationEnabled;

	public ComplianceEncryptionBuilder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		Services = services;
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }

	/// <inheritdoc />
	public IComplianceEncryptionBuilder WithEncryption(Action<AesGcmEncryptionOptions>? configure = null)
	{
		ConfigureOptions(configure);
		return this;
	}

	/// <inheritdoc />
	public IComplianceEncryptionBuilder WithInMemoryKeyManagement(Action<InMemoryKeyManagementOptions>? configure = null)
	{
		EnsureKeyManagementNotAlreadySelected(KeyManagementSelection.InMemory);
		_keyManagement = KeyManagementSelection.InMemory;

		// Naming this method IS the host stating it accepts keys that do not survive a restart, and the
		// option below records that choice so it stays distinguishable from a silent fallback. The startup
		// durability gate installed by Build() READS this option: it is a real, enforced opt-out — without
		// it a volatile provider fails startup.
		_ = Services.Configure<KeyDurabilityOptions>(o => o.AllowVolatileKeyProvider = true);

		ConfigureOptions(configure);
		return this;
	}

	/// <inheritdoc />
	public IComplianceEncryptionBuilder WithKeyManagement<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TKeyManagement>()
		where TKeyManagement : class, IKeyManagementProvider
	{
		EnsureKeyManagementNotAlreadySelected(KeyManagementSelection.Custom);
		_keyManagement = KeyManagementSelection.Custom;
		_registerCustomKeyManagement = static services =>
		{
			services.TryAddSingleton<IKeyManagementProvider, TKeyManagement>();
			if (typeof(IKeyManagementAdmin).IsAssignableFrom(typeof(TKeyManagement)))
			{
				services.TryAddSingleton<IKeyManagementAdmin>(sp => (IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());
			}
		};
		return this;
	}

	/// <inheritdoc />
	public IComplianceEncryptionBuilder WithKeyRotation(Action<RotatingEncryptionOptions>? configure = null)
	{
		_keyRotationEnabled = true;
		ConfigureOptions(configure);
		return this;
	}

	/// <summary>
	/// Materializes the accumulated configuration into DI registrations. Defaults (AES-256-GCM encryption, in-memory
	/// key management, FIPS validation) are applied for anything not explicitly selected.
	/// </summary>
	public void Build()
	{
		RegisterEncryptionOptions();
		RegisterKeyManagement();
		RegisterEncryptionProvider();
		RegisterFipsValidation();

		// Compliance encryption is a durability-REQUIRING composition: keys that vanish on restart make every
		// value encrypted under them unrecoverable. Install the startup gate here so it fires on every host
		// that composed encryption. It resolves the provider that actually won registration and refuses a
		// volatile one UNLESS the host opted out explicitly (WithInMemoryKeyManagement sets
		// AllowVolatileKeyProvider=true — the deliberate dev escape). The bare default (no key-management
		// choice) leaves AllowVolatile=false + an in-memory provider, so it fails closed at startup.
		Services.AddKeyDurabilityGate();
	}

	private void RegisterEncryptionOptions()
	{
		// Always validated at startup (Microsoft bar); the raw instance is exposed for the provider ctors that take it.
		Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AesGcmEncryptionOptions>, AesGcmEncryptionOptionsValidator>());
		Services.AddOptions<AesGcmEncryptionOptions>().ValidateOnStart();
		Services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<AesGcmEncryptionOptions>>().Value);
	}

	private void RegisterKeyManagement()
	{
		if (_keyManagement == KeyManagementSelection.Custom)
		{
			_registerCustomKeyManagement!(Services);
			return;
		}

		// Default (and explicit in-memory): provide-if-absent in-memory provider.
		Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<InMemoryKeyManagementOptions>, InMemoryKeyManagementOptionsValidator>());
		Services.AddOptions<InMemoryKeyManagementOptions>().ValidateOnStart();
		Services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<InMemoryKeyManagementOptions>>().Value);
		// This in-memory provider loses key material on restart and emits no durability attestation. The
		// startup gate installed in Build() REFUSES this default (AllowVolatileKeyProvider stays false unless
		// the host called WithInMemoryKeyManagement, which opts out explicitly) — a host that needs key
		// material to survive a restart must register a durable provider, and encrypted data is unrecoverable
		// without one.
		Services.TryAddSingleton<InMemoryKeyManagementProvider>();
		Services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<InMemoryKeyManagementProvider>());
		Services.TryAddSingleton<IKeyManagementAdmin>(sp => sp.GetRequiredService<InMemoryKeyManagementProvider>());
	}

	private void RegisterEncryptionProvider()
	{
		Services.TryAddSingleton<AesGcmEncryptionProvider>();

		if (_keyRotationEnabled)
		{
			Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RotatingEncryptionOptions>, RotatingEncryptionOptionsValidator>());
			Services.AddOptions<RotatingEncryptionOptions>().ValidateOnStart();
			Services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<RotatingEncryptionOptions>>().Value);
			Services.TryAddSingleton(sp => new RotatingEncryptionProvider(
				sp.GetRequiredService<AesGcmEncryptionProvider>(),
				sp.GetRequiredService<IKeyManagementProvider>(),
				sp.GetRequiredService<ILogger<RotatingEncryptionProvider>>(),
				sp.GetRequiredService<RotatingEncryptionOptions>()));
			Services.TryAddSingleton<IEncryptionProvider>(sp => sp.GetRequiredService<RotatingEncryptionProvider>());
			return;
		}

		Services.TryAddSingleton<IEncryptionProvider>(sp => sp.GetRequiredService<AesGcmEncryptionProvider>());
	}

	private void RegisterFipsValidation()
	{
		// FIPS validation is always registered (a security-relevant default, not an opt-in).
		Services.TryAddSingleton<IFipsDetector, DefaultFipsDetector>();
		Services.TryAddSingleton<FipsValidationService>();
	}

	private void ConfigureOptions<TOptions>(Action<TOptions>? configure)
		where TOptions : class
	{
		if (configure is not null)
		{
			_ = Services.Configure(configure);
		}
	}

	private void EnsureKeyManagementNotAlreadySelected(KeyManagementSelection requested)
	{
		if (_keyManagement != KeyManagementSelection.None && _keyManagement != requested)
		{
			throw new InvalidOperationException(
				"A key-management provider has already been selected. Call either WithInMemoryKeyManagement or " +
				"WithKeyManagement<T> exactly once, not both.");
		}
	}
}
