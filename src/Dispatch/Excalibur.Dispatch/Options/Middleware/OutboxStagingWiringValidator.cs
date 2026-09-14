// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Validates, at startup, that a host which deliberately seated outbox staging also registered the
/// <see cref="IOutboxStore"/> that staging requires.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists as a startup validator rather than a constructor guard.</b> Seating the middleware and
/// registering a store are two separate acts, and a host can perform the first without the second --
/// <c>UseOutbox()</c> adds the pipeline step and registers no store. The constructor previously carried this
/// check, but the middleware is scoped, so it fired on the first dispatch of each scope: the consumer learned
/// at request time what they could have learned at deploy time. Refusing to start is the framework idiom for a
/// configuration that cannot be honoured, and it is what the sibling capability validators in this assembly
/// already do.
/// </para>
/// <para>
/// <b>Why it is registered by <c>UseOutbox()</c> and not unconditionally.</b> The deliberate call IS the
/// signal. A host that never asked for outbox staging may have it seated by the default pipeline, and for that
/// host the absence of a store is not a misconfiguration -- it simply has no outbox, and staging stays inert.
/// Registering this validator unconditionally would fail every zero-configuration host, which is the defect
/// this arrangement exists to avoid. One constructor cannot tell those two populations apart; the registration
/// site can, because only one of them calls <c>UseOutbox()</c>.
/// </para>
/// <para>
/// <b>The predicate matches what the middleware actually receives.</b> It asks the container for a plain
/// <see cref="IOutboxStore"/>, which is exactly what the middleware's own constructor parameter resolves, so a
/// store registered under a key the middleware cannot see is correctly reported as absent rather than passing
/// a check the middleware then fails.
/// </para>
/// </remarks>
internal sealed class OutboxStagingWiringValidator : IValidateOptions<OutboxStagingOptions>
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxStagingWiringValidator"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// The application's service provider, used to ask whether a store is resolvable. Taken rather than the
	/// store itself: this validator's whole purpose is to report the store's ABSENCE, and a constructor
	/// parameter of the missing type could not be satisfied to say so.
	/// </param>
	public OutboxStagingWiringValidator(IServiceProvider serviceProvider) =>
		_serviceProvider = serviceProvider;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OutboxStagingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// A host that turned staging off asked for nothing, so there is nothing to be missing.
		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		if (_serviceProvider.GetService<IOutboxStore>() is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Outbox staging is enabled and seated in the dispatch pipeline, but no IOutboxStore is registered, " +
			"so a handler writing through IOutboxWriter would have nowhere to stage its messages. Register an " +
			"outbox store for your provider -- for example AddSqlServerOutbox(), AddPostgresOutbox() or the " +
			"equivalent for the store you use -- alongside the UseOutbox() call that seated this middleware. " +
			"If you did not intend to use the outbox, remove the UseOutbox() call or set OutboxStagingOptions." +
			"Enabled to false; the default pipeline leaves staging inert when no store is present, so no " +
			"registration is required for a host that does not stage.");
	}
}
