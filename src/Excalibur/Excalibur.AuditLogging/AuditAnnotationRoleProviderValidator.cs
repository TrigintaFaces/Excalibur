// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.AuditLogging;

/// <summary>
/// Refuses to start a host that registers audit annotations without the role provider the access checks
/// depend on.
/// </summary>
/// <remarks>
/// <para>
/// Annotation reads and writes are gated on the caller's role, and the role can only come from the consumer:
/// it is a property of their identity system, so this framework ships no implementation and cannot invent a
/// default. Every candidate default is wrong — the highest role reproduces the disclosure the gate exists to
/// prevent, the lowest silently disables the feature, and anything between invents an authorisation decision
/// on the consumer's behalf.
/// </para>
/// <para>
/// The check therefore fails the host rather than degrading. A security control whose absence is
/// indistinguishable from its presence is not a control, and a missing registration would otherwise surface
/// as a denied read in production long after the omission was made.
/// </para>
/// <para>
/// It asserts against the finished container rather than the descriptor list, so registration order cannot
/// change the outcome: a role provider registered after the annotation services still satisfies it.
/// </para>
/// </remarks>
internal sealed class AuditAnnotationRoleProviderValidator : IHostedService
{
	private readonly IServiceProviderIsService _isService;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditAnnotationRoleProviderValidator"/> class.
	/// </summary>
	/// <param name="isService">The resolved container's service-presence probe.</param>
	/// <exception cref="ArgumentNullException"><paramref name="isService"/> is <see langword="null"/>.</exception>
	public AuditAnnotationRoleProviderValidator(IServiceProviderIsService isService)
	{
		ArgumentNullException.ThrowIfNull(isService);

		_isService = isService;
	}

	/// <summary>
	/// Verifies that a role provider is registered alongside the annotation store.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the start should be canceled.</param>
	/// <returns>A completed task when the configuration is permitted.</returns>
	/// <exception cref="InvalidOperationException">
	/// Audit annotations are registered but no <see cref="IAuditRoleProvider"/> is, so the access checks
	/// cannot run.
	/// </exception>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (_isService.IsService(typeof(IAuditAnnotationStore))
			&& !_isService.IsService(typeof(IAuditRoleProvider)))
		{
			throw new InvalidOperationException(
				$"Audit annotations are registered but no {nameof(IAuditRoleProvider)} implementation is. "
				+ "Annotation access is decided by the caller's role, which only the host can supply, so no "
				+ "default is provided: choosing one would either disclose every actor's private annotations "
				+ "or silently deny all reads. Register an implementation — for example "
				+ $"services.AddScoped<{nameof(IAuditRoleProvider)}, MyRoleProvider>() — or do not register "
				+ "audit annotations.");
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Does nothing; the guard holds no resources.
	/// </summary>
	/// <param name="cancellationToken">Propagates notification that the shutdown should no longer be graceful.</param>
	/// <returns>A completed task.</returns>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
