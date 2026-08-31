// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Verifies every registered erasure store's backing schema at host startup. A store that is absent or
/// stale throws, failing host startup, so a mis-provisioned deployment can never accept an erasure request
/// — the fail-before-first-request half of the schema handshake. The per-store first-use check remains the
/// fail-closed floor for host-less (for example serverless) consumers that never run <see cref="StartAsync"/>.
/// </summary>
internal sealed class ErasureSchemaValidationHostedService : IHostedService
{
	private readonly IEnumerable<IErasureSchemaValidator> _validators;

	public ErasureSchemaValidationHostedService(IEnumerable<IErasureSchemaValidator> validators)
		=> _validators = validators;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		foreach (var validator in _validators)
		{
			await validator.ValidateSchemaAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
