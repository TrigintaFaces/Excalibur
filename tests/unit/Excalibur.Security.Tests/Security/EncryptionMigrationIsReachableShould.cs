// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Security.Tests.Security;

/// <summary>
/// Locks that encryption-version migration is REACHABLE from a composition method.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EncryptionMigrationService"/> was the last unreachable piece of a deleted seam: a public,
/// complete implementation that no composition method supplied, so a consumer could see the type and
/// could not obtain it. This asserts resolution from a real container, not the presence of a
/// registration descriptor — a descriptor whose dependencies cannot be satisfied resolves to nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class EncryptionMigrationIsReachableShould
{
	[Fact]
	public void ResolveTheMigrationServiceFromTheCompositionMethod()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The encryption provider is consumer-supplied, as the composition method documents.
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());
		_ = services.AddEncryptionMigration();

		using var provider = services.BuildServiceProvider();

		var migration = provider.GetRequiredService<IEncryptionMigrationService>();

		_ = migration.ShouldBeOfType<EncryptionMigrationService>();
	}

	[Fact]
	public void LeaveTheMigrationServiceUnregisteredUntilTheConsumerAsksForIt()
	{
		// LIVENESS for the opt-in contract: a registration that fired unconditionally would satisfy the
		// arm above while forcing an encryption provider on every host that touches this package.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IEncryptionProvider>());

		using var provider = services.BuildServiceProvider();

		provider.GetService<IEncryptionMigrationService>().ShouldBeNull();
	}
}
