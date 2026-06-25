// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Tests.Security;

/// <summary>
/// Regression lock for <c>bd-f9cn09</c> (Sprint 847, Lane F1 — signing DI fail-loud): when message
/// signing is enabled but no <see cref="IKeyProvider"/> is registered, the host MUST fail loud at startup
/// with actionable guidance — never silently inert, never a deferred first-resolve crash, never a
/// fabricated key.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (author ≠ impl). The failure contract is pinned by the
/// SoftwareArchitect (MS-F1 / 15030, reconciled 15073): a startup guard
/// (<see cref="SigningKeyProviderStartupValidator"/>, an <see cref="IHostedService"/>) that throws
/// <see cref="InvalidOperationException"/> with an <c>"IKeyProvider"</c> registration message at
/// <see cref="IHostedService.StartAsync"/>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the true pre-fix parent):</b> driven through the stable <c>IHostedService</c>
/// seam. Pre-fix, <c>AddMessageSigning</c> registered no startup validator, so host start did NOT throw
/// (signing was silently inert until a deferred first-resolve crash). Post-fix the guard fails loud.
/// RED pre-fix, GREEN post-fix.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AddMessageSigningKeyProviderShould
{
	[Fact]
	public async Task FailLoudAtStartup_WhenSigningEnabledButNoKeyProviderRegistered()
	{
		// Arrange — signing registered (Enabled defaults true) with NO IKeyProvider.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMessageSigning();

		await using var provider = services.BuildServiceProvider();
		var hostedServices = provider.GetServices<IHostedService>().ToList();

		// Act / Assert — simulate host start; the startup guard must fail loud with guidance.
		// RED on pre-fix HEAD: no startup validator was registered, so nothing throws at start.
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			foreach (var hostedService in hostedServices)
			{
				await hostedService.StartAsync(CancellationToken.None);
			}
		});

		// Bind the GUIDED message (names IKeyProvider + tells the consumer to register one), not a generic
		// unresolved-dependency error.
		exception.Message.ShouldContain("IKeyProvider");
		exception.Message.ShouldContain("register");
	}
}
