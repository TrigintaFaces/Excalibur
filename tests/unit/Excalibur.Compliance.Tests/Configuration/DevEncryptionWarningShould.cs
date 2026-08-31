// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;

namespace Excalibur.Compliance.Tests.Configuration;

/// <summary>
/// Binds the guarantee that development-only encryption announces itself at host startup, so the
/// one safety net against shipping in-memory keys to production is not inert.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class DevEncryptionWarningShould
{
	// SAFETY: the warning is actually EMITTED at startup. Asserted against a captured logger, not
	// against the presence of a registration. RED while the warning type is only ever registered
	// and never resolved.
	[Fact]
	public async Task BeEmittedAtHostStartupNotMerelyRegistered()
	{
		var capture = new CapturingLoggerProvider();
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Trace));

		_ = services.AddDevEncryption();

		await using var sp = services.BuildServiceProvider();

		// Start the host exactly as a real application would; nothing else resolves the warning.
		foreach (var hosted in sp.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}

		capture.Entries.ShouldContain(
			e => e.Level == LogLevel.Warning && e.Message.Contains("DEV ENCRYPTION IS ACTIVE", StringComparison.Ordinal),
			$"Expected a startup warning. Captured: {string.Join(" | ", capture.Entries.Select(e => $"{e.Level}:{e.Message}"))}");
	}

	// LIVENESS: the warning is specific to the development composition — a production composition
	// must NOT emit it, or the warning carries no signal.
	[Fact]
	public async Task NotBeEmittedForANonDevelopmentComposition()
	{
		var capture = new CapturingLoggerProvider();
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(capture).SetMinimumLevel(LogLevel.Trace));

		_ = services.AddHkdfKeyDerivation();

		await using var sp = services.BuildServiceProvider();
		foreach (var hosted in sp.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}

		capture.Entries.ShouldNotContain(
			e => e.Message.Contains("DEV ENCRYPTION IS ACTIVE", StringComparison.Ordinal));
	}
}
