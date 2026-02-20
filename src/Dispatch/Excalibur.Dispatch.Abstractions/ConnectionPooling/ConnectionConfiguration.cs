// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Configuration for a specific connection type.
/// </summary>
/// <typeparam name="TConnection"> The type of connection to pool. </typeparam>
public sealed class ConnectionConfiguration<TConnection>
	where TConnection : class
{
	/// <summary>
	/// Gets or sets the connection factory.
	/// </summary>
	/// <value> The factory used to create new connections. </value>
	public Func<CancellationToken, ValueTask<TConnection>>? ConnectionFactory { get; set; }

	/// <summary>
	/// Gets or sets the connection validator.
	/// </summary>
	/// <value> The validator delegate or <see langword="null" />. </value>
	public Func<TConnection, CancellationToken, ValueTask<bool>>? ConnectionValidator { get; set; }

	/// <summary>
	/// Gets or sets the connection disposal action.
	/// </summary>
	/// <value> The disposal delegate or <see langword="null" />. </value>
	public Func<TConnection, CancellationToken, ValueTask>? ConnectionDisposal { get; set; }

	/// <summary>
	/// Gets or sets the connection health checker.
	/// </summary>
	/// <value> The health check delegate or <see langword="null" />. </value>
	public Func<TConnection, CancellationToken, ValueTask<bool>>? HealthChecker { get; set; }

	/// <summary>
	/// Gets or sets the maximum lifetime of a connection.
	/// </summary>
	/// <value> The maximum lifetime or <see langword="null" /> when unlimited. </value>
	public TimeSpan? MaxLifetime { get; set; }

	/// <summary>
	/// Gets or sets the priority of this connection configuration.
	/// </summary>
	/// <value> The relative priority used when multiple configurations exist. </value>
	public int Priority { get; set; }
}
