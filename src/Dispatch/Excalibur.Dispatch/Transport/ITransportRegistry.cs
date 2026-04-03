// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Registry for managing transport adapters and their configurations.
/// </summary>
/// <remarks>
/// <para>
/// Transport packages use this interface to register their adapters during DI composition.
/// Runtime code uses it to look up adapters by name.
/// </para>
/// <para>
/// The default implementation is registered as a singleton via
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry"/>.
/// </para>
/// </remarks>
public interface ITransportRegistry
{
	/// <summary>
	/// Registers a transport adapter.
	/// </summary>
	/// <param name="name">The transport name.</param>
	/// <param name="adapter">The transport adapter.</param>
	/// <param name="transportType">The transport type identifier.</param>
	/// <param name="options">Optional transport options.</param>
	void RegisterTransport(string name, ITransportAdapter adapter, string transportType, Dictionary<string, object>? options = null);

	/// <summary>
	/// Registers a transport adapter factory for deferred creation.
	/// </summary>
	/// <param name="name">The transport name.</param>
	/// <param name="transportType">The transport type identifier.</param>
	/// <param name="factory">Factory function to create the adapter at runtime.</param>
	void RegisterTransportFactory(string name, string transportType, Func<IServiceProvider, ITransportAdapter> factory);

	/// <summary>
	/// Gets a transport adapter by name.
	/// </summary>
	/// <param name="name">The transport name.</param>
	/// <returns>The transport adapter, or <see langword="null"/> if not found.</returns>
	ITransportAdapter? GetTransportAdapter(string name);

	/// <summary>
	/// Gets all registered transport names, including pending factories.
	/// </summary>
	/// <returns>Collection of transport names (both initialized and pending).</returns>
	IEnumerable<string> GetTransportNames();

	/// <summary>
	/// Sets the default transport by name.
	/// </summary>
	/// <param name="name">The name of the transport to set as default.</param>
	/// <exception cref="InvalidOperationException">Thrown when the specified transport is not registered.</exception>
	void SetDefaultTransport(string name);
}
