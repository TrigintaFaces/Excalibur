// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Builds a real <see cref="IServiceScopeFactory"/> over the per-caller services a store resolves.
/// </summary>
/// <remarks>
/// A real container, not a faked <see cref="IServiceProvider"/>. The stores resolve through
/// <c>CreateAsyncScope</c> and <c>GetRequiredService</c>, so a fake provider would answer every request
/// with null and the tests would exercise the absent-provider branch while appearing to test the
/// populated one. Scoped is the lifetime the package's own registration helper uses.
/// </remarks>
internal static class TestScopeFactory
{
	public static IServiceScopeFactory For(
		IAuditRoleProvider? roleProvider = null,
		IAuditLogger? metaAuditLogger = null,
		IAuditActorProvider? actorProvider = null)
	{
		var services = new ServiceCollection();

		if (roleProvider is not null)
		{
			_ = services.AddScoped(_ => roleProvider);
		}

		if (metaAuditLogger is not null)
		{
			_ = services.AddScoped(_ => metaAuditLogger);
		}

		if (actorProvider is not null)
		{
			_ = services.AddScoped(_ => actorProvider);
		}

		return services
			.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
			.GetRequiredService<IServiceScopeFactory>();
	}
}
