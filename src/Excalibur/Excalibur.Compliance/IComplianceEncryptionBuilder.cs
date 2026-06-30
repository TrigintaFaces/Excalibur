// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance;

/// <summary>
/// Configures compliance encryption services (AES-256-GCM by default) in a fluent, composable way,
/// following the <c>IDataProtectionBuilder</c> idiom. Obtain an instance via
/// <c>services.AddComplianceEncryption(builder =&gt; ...)</c>.
/// </summary>
/// <remarks>
/// FIPS validation services are always registered. A default in-memory key-management provider and the
/// AES-256-GCM encryption provider are registered unless overridden by the corresponding <c>With*</c> call.
/// </remarks>
public interface IComplianceEncryptionBuilder
{
	/// <summary>
	/// Gets the underlying service collection so additional services can be registered against the same container.
	/// </summary>
	/// <value>The service collection being configured.</value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Configures the AES-256-GCM encryption provider. When omitted, the provider is registered with default options.
	/// </summary>
	/// <param name="configure">Optional configuration for the AES-256-GCM encryption options.</param>
	/// <returns>The same builder instance for chaining.</returns>
	IComplianceEncryptionBuilder WithEncryption(Action<AesGcmEncryptionOptions>? configure = null);

	/// <summary>
	/// Uses the in-memory key-management provider (suitable for development and testing). This is the default when no
	/// key-management provider is selected; calling it explicitly lets you configure its options.
	/// </summary>
	/// <param name="configure">Optional configuration for the in-memory key-management options.</param>
	/// <returns>The same builder instance for chaining.</returns>
	IComplianceEncryptionBuilder WithInMemoryKeyManagement(Action<InMemoryKeyManagementOptions>? configure = null);

	/// <summary>
	/// Uses a custom key-management provider (for example a cloud KMS). Mutually exclusive with
	/// <see cref="WithInMemoryKeyManagement"/>.
	/// </summary>
	/// <typeparam name="TKeyManagement">The key-management provider implementation type.</typeparam>
	/// <returns>The same builder instance for chaining.</returns>
	IComplianceEncryptionBuilder WithKeyManagement<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TKeyManagement>()
		where TKeyManagement : class, IKeyManagementProvider;

	/// <summary>
	/// Enables key rotation, wrapping the encryption provider so encrypted data is transparently re-encrypted
	/// under the current key version.
	/// </summary>
	/// <param name="configure">Optional configuration for the rotating-encryption options.</param>
	/// <returns>The same builder instance for chaining.</returns>
	IComplianceEncryptionBuilder WithKeyRotation(Action<RotatingEncryptionOptions>? configure = null);
}
