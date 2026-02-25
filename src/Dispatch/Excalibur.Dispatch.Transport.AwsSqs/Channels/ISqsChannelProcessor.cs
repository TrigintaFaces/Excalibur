// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Interface for SQS channel processor.
/// </summary>
public interface ISqsChannelProcessor
{
	/// <summary>
	/// Starts the processor.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops the processor.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
