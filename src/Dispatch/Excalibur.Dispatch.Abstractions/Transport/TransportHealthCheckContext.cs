// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Context for transport health check operations.
/// </summary>
/// <remarks>
/// Creates a new transport health check context.
/// </remarks>
/// <param name="requestedCategories">The categories of health checks to perform.</param>
/// <param name="timeout">The timeout for the health check operation.</param>
public sealed class TransportHealthCheckContext(
	TransportHealthCheckCategory requestedCategories,
	TimeSpan? timeout = null)
{
	/// <summary>
	/// Gets the categories of health checks to perform.
	/// </summary>
	public TransportHealthCheckCategory RequestedCategories { get; } = requestedCategories;

	/// <summary>
	/// Gets the timeout for the health check operation.
	/// </summary>
	public TimeSpan Timeout { get; } = timeout ?? TimeSpan.FromSeconds(30);
}
