// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Validates <see cref="OutboxStagingOptions"/> at startup via <c>ValidateOnStart</c>.
/// </summary>
/// <remarks>
/// <b>This guard is deliberately narrower than the choice it appears to govern, and the gap is structural.</b>
/// It confirms the parts THIS package can see -- an outbox store and the transaction middleware. Whether an
/// event store can stage transactionally is declared by an interface in the event-sourcing packages, which
/// this package does not reference and must not: the dependency runs one way, from event sourcing to
/// dispatch. So a success here means "the dispatch-side prerequisites are present", never "your writes will
/// be transactional" -- a configuration can satisfy this validator and still resolve to an
/// eventually-consistent path at runtime. The messages below say so rather than leaving a consumer to infer
/// a guarantee from a silent pass.
/// </remarks>
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
					"Register one via AddCosmosDbOutbox(), AddSqlServerOutbox(), etc. " +
					"Note that this setting is a startup requirement rather than a write-path selector: " +
					"for event-sourced aggregates the path is chosen by the staging strategy on the " +
					"event-sourcing builder.");
			}

			var middleware = serviceProvider.GetServices<IDispatchMiddleware>();
			if (!middleware.OfType<TransactionMiddleware>().Any())
			{
				return ValidateOptionsResult.Fail(
					"Transactional outbox consistency requires TransactionMiddleware in the pipeline. " +
					"Call .UseTransaction() on the dispatch builder. " +
					"Note that this setting is a startup requirement rather than a write-path selector: " +
					"for event-sourced aggregates the path is chosen by the staging strategy on the " +
					"event-sourcing builder.");
			}
		}

		// Success means the dispatch-side prerequisites are present. It does NOT mean writes will be
		// transactional: this package cannot see whether the event store supports transactional staging, so
		// a configuration that passes here can still resolve to an eventually-consistent path.
		return ValidateOptionsResult.Success;
	}
}
