// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
using Excalibur.Dispatch.Middleware.Transaction;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Validates <see cref="OutboxStagingOptions"/> at startup via <c>ValidateOnStart</c>.
/// </summary>
internal sealed class OutboxStagingOptionsValidator(IServiceProvider serviceProvider)
	: IValidateOptions<OutboxStagingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxStagingOptions options)
	{
		if (options.ConsistencyMode == OutboxConsistencyMode.Transactional)
		{
			var outboxStore = serviceProvider.GetKeyedService<IOutboxStore>("default");
			if (outboxStore is null)
			{
				return ValidateOptionsResult.Fail(
					"Transactional outbox consistency requires a registered IOutboxStore. " +
					"Register one via AddCosmosDbOutbox(), AddSqlServerOutbox(), etc.");
			}

			var middleware = serviceProvider.GetServices<IDispatchMiddleware>();
			if (!middleware.OfType<TransactionMiddleware>().Any())
			{
				return ValidateOptionsResult.Fail(
					"Transactional outbox consistency requires TransactionMiddleware in the pipeline. " +
					"Call .UseTransaction() on the dispatch builder.");
			}
		}

		return ValidateOptionsResult.Success;
	}
}
