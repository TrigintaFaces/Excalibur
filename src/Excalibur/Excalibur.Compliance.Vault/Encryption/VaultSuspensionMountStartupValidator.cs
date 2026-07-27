// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Vault;

/// <summary>
/// Startup guard that fails loud when the Vault KV&#160;v2 mount required for durable key-suspension markers
/// is not reachable.
/// </summary>
/// <remarks>
/// Vault Transit has no native "disable key" primitive, so key suspension is enforced by writing a durable
/// marker into the KV&#160;v2 mount named by <see cref="VaultSuspensionOptions.MountPath"/> and reading it
/// back when a key's status is resolved. That mount is therefore a hard prerequisite for suspension: if it is
/// absent or unreachable, the marker can be neither written nor read, and a suspended key could silently
/// appear active (a fail-open security hole). This guard runs a read-only probe at host start
/// (<see cref="IHostedService.StartAsync"/>) and throws <see cref="InvalidOperationException"/> with
/// actionable guidance when the mount is not reachable — surfacing the misconfiguration immediately rather
/// than letting suspension become silently inert at runtime. The probe never creates the mount or writes any
/// data; anything short of a positively reachable mount fails closed.
/// </remarks>
internal sealed class VaultSuspensionMountStartupValidator(
	VaultKeyProvider provider,
	IOptions<VaultOptions> options) : IHostedService
{
	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			await provider.ValidateSuspensionMountReachableAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			var suspension = options.Value.Suspension;
			throw new InvalidOperationException(
				$"The Vault KV v2 mount '{suspension.MountPath}' required for durable key-suspension markers " +
				$"is not reachable. Key suspension writes/reads a marker at '{suspension.MountPath}/{suspension.Path}/{{keyId}}' " +
				"and cannot be enforced without it — a suspended key would otherwise appear active (fail-open). " +
				"Mount a KV v2 secrets engine at that path and grant the provider's token create/read access to it, " +
				"or set VaultOptions.Suspension.MountPath to an existing KV v2 mount.",
				ex);
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
