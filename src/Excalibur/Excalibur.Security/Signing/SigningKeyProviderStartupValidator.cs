// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Security;

/// <summary>
/// Startup guard that fails loud when message signing is enabled but no <see cref="IKeyProvider"/> is
/// registered.
/// </summary>
/// <remarks>
/// Runs at host start (<see cref="IHostedService.StartAsync"/>) and throws
/// <see cref="InvalidOperationException"/> with actionable guidance, so a missing key provider surfaces
/// immediately rather than as a deferred first-resolve crash of the <see cref="IKeyProvider"/>-dependent
/// signing service (FR-4 / <c>f9cn09</c>). The validated condition is a missing DI registration — not an
/// invalid options value — so the idiomatic failure is the same <see cref="InvalidOperationException"/>
/// that <c>GetRequiredService</c> raises, not an options-validation failure. An <see cref="IKeyProvider"/>
/// is a required deployment decision the consumer must make explicitly; the framework never silently
/// fabricates signing keys.
/// </remarks>
internal sealed class SigningKeyProviderStartupValidator(
	IOptions<SigningOptions> options,
	IEnumerable<IKeyProvider> keyProviders) : IHostedService
{
	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		// A disabled signing pipeline needs no key provider.
		if (options.Value.Enabled && !keyProviders.Any())
		{
			throw new InvalidOperationException(
				"No IKeyProvider is registered. Message signing requires a key provider — register one " +
				"(a configuration/secret-backed provider, Excalibur.Security.Azure, or Excalibur.Security.Aws).");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
