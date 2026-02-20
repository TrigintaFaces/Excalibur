// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines a component that supports health checking.
/// </summary>
public interface IHealthCheckable
{
	/// <summary>
	/// Performs a health check on the component.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the health check operation. </param>
	/// <returns> A task representing the health check result indicating whether the component is healthy. </returns>
	Task<bool> CheckHealthAsync(CancellationToken cancellationToken);
}
