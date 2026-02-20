// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Service for registering poison message detection rules.
/// </summary>
public sealed class PoisonRuleRegistrationService : IHostedService
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PoisonRuleRegistrationService" /> class.
	/// </summary>
	public PoisonRuleRegistrationService()
	{
	}

	/// <summary>
	/// Starts the service.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task StartAsync(CancellationToken cancellationToken) =>

		// Registration happens during construction in the factory method
		Task.CompletedTask;

	/// <summary>
	/// Stops the service.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
