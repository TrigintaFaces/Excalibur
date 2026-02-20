// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Factory for creating and managing persistence providers.
/// </summary>
public interface IPersistenceProviderFactory
{
	/// <summary>
	/// Creates a persistence provider of the specified type.
	/// </summary>
	/// <typeparam name="TProvider"> The type of the persistence provider. </typeparam>
	/// <param name="name"> The name of the provider configuration to use. </param>
	/// <returns> The created persistence provider. </returns>
	TProvider CreateProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(string name)
		where TProvider : IPersistenceProvider;

	/// <summary>
	/// Creates a persistence provider using the default configuration.
	/// </summary>
	/// <typeparam name="TProvider"> The type of the persistence provider. </typeparam>
	/// <returns> The created persistence provider. </returns>
	TProvider CreateProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
		where TProvider : IPersistenceProvider;

	/// <summary>
	/// Gets a cached persistence provider by name.
	/// </summary>
	/// <param name="name"> The name of the provider. </param>
	/// <returns> The cached provider if it exists; otherwise, null. </returns>
	IPersistenceProvider? GetProvider(string name);

	/// <summary>
	/// Gets all registered provider names.
	/// </summary>
	/// <returns> Collection of registered provider names. </returns>
	IEnumerable<string> GetProviderNames();

	/// <summary>
	/// Registers a provider instance with the factory.
	/// </summary>
	/// <param name="name"> The name to register the provider under. </param>
	/// <param name="provider"> The provider instance. </param>
	void RegisterProvider(string name, IPersistenceProvider provider);

	/// <summary>
	/// Unregisters a provider from the factory.
	/// </summary>
	/// <param name="name"> The name of the provider to unregister. </param>
	/// <returns> True if the provider was unregistered; otherwise, false. </returns>
	bool UnregisterProvider(string name);

	/// <summary>
	/// Disposes all registered providers.
	/// </summary>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task DisposeAllProvidersAsync();
}
