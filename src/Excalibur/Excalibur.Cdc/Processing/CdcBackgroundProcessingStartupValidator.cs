// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Cdc.Processing;

/// <summary>
/// Validates, at host start, that a <see cref="ICdcBackgroundProcessor"/> is available when CDC
/// background processing is enabled, and fails fast with an actionable message when none is registered.
/// </summary>
/// <remarks>
/// <para>
/// This check runs at host start rather than at service registration so that composing the CDC builder
/// (for example in configuration or unit tests that never build and start a host) does not throw. Because
/// it resolves the full container at start, the check is order-independent: it does not matter whether the
/// provider that supplies the processor was registered before or after CDC.
/// </para>
/// <para>
/// Without this guard, a missing processor surfaces later as an obscure dependency-injection activation
/// failure when the host constructs <see cref="CdcProcessingHostedService"/>.
/// </para>
/// </remarks>
internal sealed class CdcBackgroundProcessingStartupValidator : IHostedService
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcBackgroundProcessingStartupValidator"/> class.
	/// </summary>
	/// <param name="services">The application service provider used to resolve registered processors.</param>
	public CdcBackgroundProcessingStartupValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_services.GetServices<ICdcBackgroundProcessor>().Any())
		{
			throw new InvalidOperationException(
				"CDC background processing was enabled via EnableBackgroundProcessing(), but no " +
				"ICdcBackgroundProcessor is registered. Background processing currently requires a " +
				"provider that supports it (for example, UseSqlServer()); other CDC providers do not " +
				"yet supply a background processor. Remove EnableBackgroundProcessing() or select a " +
				"provider that supports background processing.");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
