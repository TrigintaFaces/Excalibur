// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Manages the lifecycle of transport adapters, providing runtime control over individual transports.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are typically registered as an <c>IHostedService</c> to automatically start
/// all registered transports during application startup and gracefully stop them during shutdown.
/// </para>
/// <para>
/// Beyond automatic lifecycle management, this interface provides runtime control for scenarios such as:
/// </para>
/// <list type="bullet">
/// <item><description>Graceful degradation - stop a failing transport while keeping others running</description></item>
/// <item><description>Dynamic scaling - start/stop transports based on load patterns</description></item>
/// <item><description>Maintenance windows - temporarily disable specific transports</description></item>
/// <item><description>Testing scenarios - control transport state during integration tests</description></item>
/// </list>
/// </remarks>
public interface ITransportLifecycleManager
{
	/// <summary>
	/// Gets all registered transport adapters.
	/// </summary>
	/// <value>
	/// A read-only collection of transport adapters that have been registered
	/// with the lifecycle manager.
	/// </value>
	/// <remarks>
	/// <para>
	/// This collection includes both running and stopped transports. Use
	/// <see cref="ITransportAdapter.IsRunning"/> to check individual transport status.
	/// </para>
	/// </remarks>
	IReadOnlyCollection<ITransportAdapter> RegisteredTransports { get; }

	/// <summary>
	/// Gets all transport names that are currently registered.
	/// </summary>
	/// <value>A read-only collection of transport names.</value>
	IReadOnlyCollection<string> TransportNames { get; }

	/// <summary>
	/// Starts a specific transport adapter by name.
	/// </summary>
	/// <param name="name">The name of the transport to start.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous start operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no transport with the specified name is registered.
	/// </exception>
	/// <remarks>
	/// <para>
	/// If the transport is already running, this method returns immediately without error.
	/// </para>
	/// </remarks>
	Task StartTransportAsync(string name, CancellationToken cancellationToken);

	/// <summary>
	/// Stops a specific transport adapter by name.
	/// </summary>
	/// <param name="name">The name of the transport to stop.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous stop operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no transport with the specified name is registered.
	/// </exception>
	/// <remarks>
	/// <para>
	/// If the transport is already stopped, this method returns immediately without error.
	/// </para>
	/// <para>
	/// The stop operation includes a drain period to allow in-flight messages to complete
	/// processing before forcing shutdown.
	/// </para>
	/// </remarks>
	Task StopTransportAsync(string name, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the transport adapter with the specified name.
	/// </summary>
	/// <param name="name">The name of the transport to retrieve.</param>
	/// <returns>
	/// The transport adapter if found; otherwise, <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
	ITransportAdapter? GetTransport(string name);

	/// <summary>
	/// Checks if a transport with the specified name is registered.
	/// </summary>
	/// <param name="name">The name of the transport to check.</param>
	/// <returns>
	/// <see langword="true"/> if a transport with the specified name is registered;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
	bool IsRegistered(string name);

	/// <summary>
	/// Checks if a transport with the specified name is currently running.
	/// </summary>
	/// <param name="name">The name of the transport to check.</param>
	/// <returns>
	/// <see langword="true"/> if the transport is registered and running;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
	bool IsRunning(string name);
}
