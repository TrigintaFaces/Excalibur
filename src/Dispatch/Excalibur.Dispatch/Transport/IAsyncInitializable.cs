// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Defines a component that supports asynchronous initialization.
/// </summary>
public interface IAsyncInitializable
{
	/// <summary>
	/// Initializes the component asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> Token to cancel the initialization operation. </param>
	/// <returns> A task representing the initialization operation. </returns>
	Task InitializeAsync(CancellationToken cancellationToken);
}
