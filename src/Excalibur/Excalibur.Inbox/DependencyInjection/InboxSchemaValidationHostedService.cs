// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.Inbox;

/// <summary>
/// Verifies every registered inbox store's physical schema against its deployment mode at host startup. A
/// mismatch throws, failing host startup, so a mis-provisioned deployment can never serve a message — the
/// fail-before-first-message half of the schema handshake. The per-store first-use check remains the
/// fail-closed floor for host-less (for example serverless) consumers that never run <see cref="StartAsync"/>.
/// </summary>
internal sealed class InboxSchemaValidationHostedService : IHostedService
{
	private readonly IEnumerable<IInboxSchemaValidator> _validators;

	public InboxSchemaValidationHostedService(IEnumerable<IInboxSchemaValidator> validators)
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
