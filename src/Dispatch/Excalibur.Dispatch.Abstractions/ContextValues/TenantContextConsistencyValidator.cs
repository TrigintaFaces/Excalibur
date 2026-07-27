// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch;

/// <summary>
/// Fail-closed startup guard that couples <see cref="TenantContextOptions.RequireTenant"/> to the ambient
/// <see cref="ITenantContext"/> registration, closing a silent cross-tenant data-loss configuration.
/// </summary>
/// <remarks>
/// <para>
/// Tenant isolation on a keyed store (inbox, saga, snapshot) is structural only when the deployment-mode
/// flag and the ambient tenant accessor agree. The multi-tenancy composition sets both together, but the
/// <see cref="ITenantContext"/> registration is open: a consumer can register a per-request resolving
/// context of their own while leaving <see cref="TenantContextOptions.RequireTenant"/> at its
/// single-tenant default. That combination applies the single-tenant (no discriminator) schema yet routes
/// two different tenants through the same keyed rows — the second write collides on the composite key and
/// is silently deduplicated, dropping one tenant's message.
/// </para>
/// <para>
/// The invariant enforced here: when <see cref="TenantContextOptions.RequireTenant"/> is
/// <see langword="false"/>, the resolved <see cref="ITenantContext"/> MUST be the framework
/// single-tenant default (<see cref="SingleTenantContext"/>). A resolving context in single-tenant mode is
/// rejected at startup — before the first message — rather than proceeding into the silent-loss state.
/// </para>
/// <para>
/// The check is lifetime-agnostic: a fresh service scope resolves the ambient context so a scoped,
/// transient, or singleton registration is inspected identically, and only its type identity is examined
/// (the ambient tenant value is never read).
/// </para>
/// </remarks>
internal sealed class TenantContextConsistencyValidator : IValidateOptions<TenantContextOptions>
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="TenantContextConsistencyValidator"/> class.
	/// </summary>
	/// <param name="serviceProvider">The root service provider used to resolve the ambient context.</param>
	public TenantContextConsistencyValidator(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

		_serviceProvider = serviceProvider;
	}

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TenantContextOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Only single-tenant mode carries the hazard: in required-tenant mode the store emits the tenant
		// discriminator, so any (resolving or default) context is safe.
		if (options.RequireTenant)
		{
			return ValidateOptionsResult.Success;
		}

		// Resolve the ambient context in a fresh scope so a scoped/transient registration is inspected the
		// same as a singleton. Only the type identity is examined; the ambient tenant value is never read.
		using var scope = _serviceProvider.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ITenantContext>();

		if (context is SingleTenantContext)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"A custom {nameof(ITenantContext)} ('{context.GetType().Name}') is registered while "
			+ $"{nameof(TenantContextOptions)}.{nameof(TenantContextOptions.RequireTenant)} is false. A "
			+ "resolving tenant context in single-tenant mode applies the single-tenant schema but routes "
			+ "multiple tenants through the same keyed rows, silently deduplicating one tenant's data "
			+ "against another's. Enable multi-tenant mode by calling AddMultiTenancy(...) (which requires "
			+ "the tenant discriminator), or remove the custom context to use the framework single-tenant "
			+ "default.");
	}
}
