// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure.DependencyInjection;

/// <summary>
/// Startup guard that fails host start when GDPR erasure is registered without a data-inventory discovery
/// source (no <see cref="IDataInventoryService"/> is wired) and key-destruction-only erasure was not
/// explicitly opted into (<see cref="ErasureOptions.KeyShredOnlyErasure"/> is <see langword="false"/>).
/// </summary>
/// <remarks>
/// A GDPR erasure completion certificate is a compliance PROOF, so it must fail closed: without a
/// discovery source, store-level coverage can never be verified, and a certificate must never attest
/// coverage it did not verify. Rather than silently issue an unprovable certificate at runtime, this guard
/// makes the misconfiguration a loud, actionable startup failure — the marker-inseparable-from-wiring
/// discipline: the ability to certify store-coverage is inseparable from having wired discovery. The check
/// inspects service <em>registration</em> via <see cref="IServiceProviderIsService"/> and never resolves
/// the probed service (this guard is a singleton holding the root provider). AOT-safe: no reflection.
/// Sibling of the encryption wiring validators. Runtime defense-in-depth is provided independently by the
/// affirmative-coverage gate in <see cref="ErasureService"/>.
/// </remarks>
internal sealed partial class ErasureDiscoverySourceValidator : IHostedService
{
	private readonly IServiceProvider _services;
	private readonly IOptions<ErasureOptions> _options;
	private readonly ILogger<ErasureDiscoverySourceValidator> _logger;

	public ErasureDiscoverySourceValidator(
		IServiceProvider services,
		IOptions<ErasureOptions> options,
		ILogger<ErasureDiscoverySourceValidator> logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// Explicit opt-in: key-destruction-only erasure accepts no store-coverage verification.
		if (_options.Value.KeyShredOnlyErasure)
		{
			return Task.CompletedTask;
		}

		var isService = _services.GetRequiredService<IServiceProviderIsService>();
		if (isService.IsService(typeof(IDataInventoryService)))
		{
			return Task.CompletedTask;
		}

		LogNoDiscoverySource();

		throw new InvalidOperationException(
			"GDPR erasure is registered without a data-inventory discovery source, so store-level coverage "
			+ "can never be verified and a completion certificate would be issued over unverified coverage. "
			+ "Register a discovery source (AddDataInventoryService) or set "
			+ "ErasureOptions.KeyShredOnlyErasure = true to explicitly accept key-destruction-only erasure "
			+ "(certificates then report a coverage basis of key destruction, not store-verified coverage).");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(LogLevel.Critical,
		"GDPR erasure is registered without a data-inventory discovery source and KeyShredOnlyErasure is not "
		+ "set — a completion certificate would be issued over UNVERIFIED store coverage. Register a discovery "
		+ "source (AddDataInventoryService) or set ErasureOptions.KeyShredOnlyErasure = true.")]
	private partial void LogNoDiscoverySource();
}
