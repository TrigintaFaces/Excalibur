// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox;

/// <summary>
/// Internal implementation of the Outbox builder.
/// </summary>
internal sealed class OutboxBuilder : IOutboxBuilder
{
	private readonly OutboxConfiguration _config;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="config">The outbox configuration to modify.</param>
	public OutboxBuilder(IServiceCollection services, OutboxConfiguration config)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc/>
	public IServiceCollection Services { get; }

	/// <summary>
	/// Gets the internal configuration for building final options.
	/// </summary>
	internal OutboxConfiguration Configuration => _config;

	/// <inheritdoc/>
	public IOutboxBuilder WithProcessing(Action<IOutboxProcessingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new OutboxProcessingBuilder(_config);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IOutboxBuilder WithCleanup(Action<IOutboxCleanupBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new OutboxCleanupBuilder(_config);
		configure(builder);

		return this;
	}

	/// <inheritdoc/>
	public IOutboxBuilder EnableBackgroundProcessing()
	{
		_config.EnableBackgroundProcessing = true;
		_ = Services.AddHostedService<OutboxBackgroundService>();
		return this;
	}
}
