// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.Outbox;
using Excalibur.Saga;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting.Builders;

/// <summary>
/// Default implementation of <see cref="IExcaliburBuilder"/> that delegates
/// to existing subsystem registration methods.
/// </summary>
/// <remarks>
/// <para>
/// Each <c>Add*</c> method delegates to the corresponding standalone
/// <c>IServiceCollection</c> extension (e.g., <c>AddExcaliburEventSourcing</c>,
/// <c>AddExcaliburOutbox</c>) so consumers get the same registration behavior
/// whether they use the unified builder or the individual methods.
/// </para>
/// </remarks>
internal sealed class ExcaliburBuilder : IExcaliburBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExcaliburBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	internal ExcaliburBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc/>
	public IServiceCollection Services { get; }

	/// <inheritdoc/>
	public IExcaliburBuilder AddEventSourcing(Action<IEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_ = Services.AddExcaliburEventSourcing(configure);
		return this;
	}

	/// <inheritdoc/>
	public IExcaliburBuilder AddOutbox(Action<IOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_ = Services.AddExcaliburOutbox(configure);
		return this;
	}

	/// <inheritdoc/>
	public IExcaliburBuilder AddCdc(Action<ICdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_ = Services.AddCdcProcessor(configure);
		return this;
	}

	/// <inheritdoc/>
	public IExcaliburBuilder AddSagas(Action<SagaOptions>? configure = null)
	{
		if (configure is not null)
		{
			_ = Services.AddExcaliburSaga(configure);
		}
		else
		{
			_ = Services.AddExcaliburSaga();
		}

		return this;
	}

	/// <inheritdoc/>
	public IExcaliburBuilder AddLeaderElection(Action<LeaderElectionOptions>? configure = null)
	{
		if (configure is not null)
		{
			_ = Services.AddExcaliburLeaderElection(configure);
		}
		else
		{
			_ = Services.AddExcaliburLeaderElection();
		}

		return this;
	}
}
