// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Centralized timeout management service to avoid hardcoded timeouts.
/// </summary>
public interface ITimeoutManager
{
	/// <summary>
	/// Gets the default timeout for all operations.
	/// </summary>
	/// <value>The fallback timeout applied when no named override exists.</value>
	TimeSpan DefaultTimeout { get; }

	/// <summary>
	/// Gets the configured timeout for the specified operation.
	/// </summary>
	/// <param name="operationName"> The name of the operation. </param>
	/// <returns> The configured timeout for the operation. </returns>
	TimeSpan GetTimeout(string operationName);

	/// <summary>
	/// Registers a custom timeout for a specific operation.
	/// </summary>
	/// <param name="operationName"> The name of the operation. </param>
	/// <param name="timeout"> The timeout duration. </param>
	void RegisterTimeout(string operationName, TimeSpan timeout);
}
