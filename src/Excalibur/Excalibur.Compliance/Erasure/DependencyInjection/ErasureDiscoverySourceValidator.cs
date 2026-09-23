// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// A GDPR erasure completion certificate is relied on as a compliance record, so it must fail closed: without a
/// discovery source, store-level coverage can never be verified, and a certificate must never attest
/// coverage it did not verify. Rather than silently issue an unprovable certificate at runtime, this guard
/// makes the misconfiguration a loud, actionable startup failure — the marker-inseparable-from-wiring
/// discipline: the ability to certify store-coverage is inseparable from having wired discovery. The check
/// inspects service <em>registration</em> via <see cref="IServiceProviderIsService"/> and never resolves
/// the probed service (this guard is a singleton holding the root provider). AOT-safe: no reflection.
/// Sibling of the encryption wiring validators. Runtime defense-in-depth is provided independently by the
/// affirmative-coverage gate in <see cref="ErasureService"/>.
/// </remarks>
internal sealed partial class ErasureDiscoverySourceValidator : IHostedService, IStartupPrerequisiteValidator
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

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		await ValidateRegistryIsNotEmptyAsync(cancellationToken).ConfigureAwait(false);
		await WarnWhenKeyDestructionCannotBeConfirmedAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Names, at startup, a key provider that cannot confirm destruction. Erasures that destroy its keys can never be
	/// certified, and without this the first sign of it would be a request that waits forever.
	/// </summary>
	private async Task WarnWhenKeyDestructionCannotBeConfirmedAsync()
	{
		await using var scope = _services.CreateAsyncScope();

		IKeyManagementProvider? provider;
		try
		{
			provider = scope.ServiceProvider.GetService<IKeyManagementProvider>();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// Constructing the provider is its own registration's concern and fails where it is first used; this
			// advisory check does not add a second, earlier failure mode for it.
			return;
		}

		if (provider is not null && provider.GetService(typeof(IKeyDestructionStatusProvider)) is null)
		{
			LogKeyProviderCannotConfirmDestruction(provider.GetType().FullName ?? provider.GetType().Name);
		}
	}

	/// <summary>
	/// Fails host start when a discovery source IS wired and the data-location registry cannot be READ.
	/// An EMPTY registry is logged and does not fail start; the two cases and their reasons are below.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This question moved here from <c>ErasureService</c>, where it was asked once per data subject. An
	/// empty registry is a property of the DEPLOYMENT, identical for every subject, so asking it per
	/// subject refused every erasure in every shipped configuration and surfaced the remedy at the one
	/// moment the operator could not act on it. Config-time questions belong at config time.
	/// </para>
	/// <para>
	/// A store that cannot be read is NOT treated as an empty registry. The two are different facts and
	/// only one of them is a misconfiguration; reporting "nothing is registered" when the truth is "I could
	/// not tell" would be the same absence-of-evidence error this guard exists to prevent. An unreadable
	/// store therefore refuses with its own message, naming the underlying failure.
	/// </para>
	/// <para>
	/// ORDERING: this runs as an <see cref="IHostedService"/>, so a host that populates the registry from a
	/// LATER hosted service is still EMPTY when this runs. That does NOT fail startup — an empty registry
	/// only logs here, precisely because a first boot is legitimately empty and refusing would deadlock the
	/// host that must start in order to register. The empty case is refused later, at certificate issuance,
	/// where a first boot and a misconfigured host are trivially distinct: a first boot has not requested an
	/// erasure. Only an UNREADABLE store fails startup, because it is never a legitimate first boot.
	/// </para>
	/// </remarks>
	private async Task ValidateRegistryIsNotEmptyAsync(CancellationToken cancellationToken)
	{
		if (_options.Value.KeyShredOnlyErasure)
		{
			return;
		}

		var isService = _services.GetRequiredService<IServiceProviderIsService>();
		if (!isService.IsService(typeof(IDataInventoryStore)))
		{
			// No store wired at all: the sibling check above already governs that configuration.
			return;
		}

		await using var scope = _services.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IDataInventoryStore>();

		IReadOnlyList<DataLocationRegistration> registrations;
		try
		{
			registrations = await store.GetAllRegistrationsAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogRegistryUnreadable(ex);

			throw new InvalidOperationException(
				"GDPR erasure is registered but the data-location registry could not be READ at startup, so "
				+ "whether any personal-data location is registered is UNKNOWN. This is NOT treated as an "
				+ "empty registry: an empty registry is a legitimate first boot, while a store that cannot "
				+ "be read is never one, and a completion certificate must not rest on a coverage question "
				+ "nobody could answer. Fix the data-inventory store, or set "
				+ "ErasureOptions.KeyShredOnlyErasure = true to accept key-destruction-only erasure.", ex);
		}

		if (registrations.Count > 0)
		{
			return;
		}

		// OBSERVES HERE, REFUSES AT ISSUANCE. Both halves are deliberate and neither is an omission.
		//
		// Refusing here breaks FIRST BOOT: a data-location registry is runtime data populated through
		// RegisterDataLocationAsync, so on a clean install it is empty BY DEFINITION and no host with
		// erasure wired could start -- it cannot start in order to run the registration that would let it
		// start. That is the same over-refusal the per-subject coverage arm was withdrawn for, one level
		// up: it would refuse every HOST instead of every SUBJECT, which is worse.
		//
		// The refusal therefore lives where a first boot and a misconfigured host are trivially distinct:
		// certificate issuance in ErasureService, which a first boot has not reached because it has not
		// requested an erasure. Refusing there refuses a CLAIM rather than a host, and this log is the
		// early, actionable warning of the same condition rather than the enforcement of it.
		LogEmptyRegistry();
	}

	public void Validate()
	{
		// Explicit opt-in: key-destruction-only erasure accepts no store-coverage verification.
		if (_options.Value.KeyShredOnlyErasure)
		{
			return;
		}

		var isService = _services.GetRequiredService<IServiceProviderIsService>();
		if (isService.IsService(typeof(IDataInventoryService)))
		{
			return;
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

	[LoggerMessage(LogLevel.Critical,
		"GDPR erasure is registered with a discovery source but the data-location registry is EMPTY — a "
		+ "completion certificate would attest to an absence of evidence. Register locations with "
		+ "RegisterDataLocationAsync, or set ErasureOptions.KeyShredOnlyErasure = true.")]
	private partial void LogEmptyRegistry();

	[LoggerMessage(LogLevel.Warning,
		"GDPR erasure is registered with key provider {ProviderType}, which does not implement "
		+ "IKeyDestructionStatusProvider. Erasure cannot confirm that its keys are destroyed, so erasures that "
		+ "depend on them stay AwaitingKeyDestruction and are never certified. Implement "
		+ "IKeyDestructionStatusProvider on the provider to let them complete.")]
	private partial void LogKeyProviderCannotConfirmDestruction(string providerType);

	[LoggerMessage(LogLevel.Critical,
		"GDPR erasure is registered but the data-location registry could not be READ at startup, so whether "
		+ "any location is registered is UNKNOWN. This is not treated as an empty registry.")]
	private partial void LogRegistryUnreadable(Exception exception);
}
