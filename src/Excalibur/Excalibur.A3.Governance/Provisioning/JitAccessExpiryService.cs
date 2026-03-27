// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Background service that periodically scans for expired JIT (just-in-time) access grants
/// and revokes them via <see cref="IGrantStore.DeleteGrantAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="PeriodicTimer"/> with the interval from <see cref="JitAccessOptions.ExpiryCheckInterval"/>
/// and <see cref="IServiceScopeFactory"/> for scoped dependencies.
/// </para>
/// </remarks>
internal sealed partial class JitAccessExpiryService(
	IServiceScopeFactory scopeFactory,
	IOptions<JitAccessOptions> options,
	ILogger<JitAccessExpiryService> logger) : BackgroundService
{
	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Resolve ProvisioningOptions to check EnableJitAccess
		using var initScope = scopeFactory.CreateScope();
		var provisioningOptions = initScope.ServiceProvider.GetService<IOptions<ProvisioningOptions>>();
		if (provisioningOptions?.Value.EnableJitAccess != true)
		{
			return;
		}

		var opts = options.Value;
		using var timer = new PeriodicTimer(opts.ExpiryCheckInterval);

		LogJitExpiryCheckStarted(logger);

		while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
		{
			try
			{
				await ScanAndRevokeExpiredGrantsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
#pragma warning disable CA1031 // Do not catch general exception types -- BackgroundService must not crash
			catch (Exception ex)
			{
				LogJitExpiryCheckFailed(logger, ex);
			}
#pragma warning restore CA1031
		}
	}

	private async Task ScanAndRevokeExpiredGrantsAsync(CancellationToken cancellationToken)
	{
		await using var scope = scopeFactory.CreateAsyncScope();
		var grantStore = scope.ServiceProvider.GetService<IGrantStore>();

		if (grantStore is null)
		{
			return;
		}

		var provisioningStore = scope.ServiceProvider.GetService<IProvisioningStore>();
		if (provisioningStore is null)
		{
			return;
		}

		// Query provisioning requests that were provisioned and have JIT expiry
		var provisionedRequests = await provisioningStore.GetRequestsByStatusAsync(
			ProvisioningRequestStatus.Provisioned, cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var revokedCount = 0;

		foreach (var request in provisionedRequests)
		{
			if (request.RequestedExpiry is null || request.RequestedExpiry > now)
			{
				continue;
			}

			// Check if the grant still exists
			var grantExists = await grantStore.GrantExistsAsync(
				request.UserId,
				request.TenantId ?? string.Empty,
				request.GrantType,
				request.GrantScope,
				cancellationToken).ConfigureAwait(false);

			if (!grantExists)
			{
				continue;
			}

			// Revoke the expired JIT grant
			await grantStore.DeleteGrantAsync(
				request.UserId,
				request.TenantId ?? string.Empty,
				request.GrantType,
				request.GrantScope,
				revokedBy: "JitAccessExpiryService",
				revokedOn: now,
				cancellationToken).ConfigureAwait(false);

			LogJitGrantExpired(logger, request.UserId, request.GrantScope);
			revokedCount++;
		}

		LogJitExpiryCheckCompleted(logger, revokedCount);
	}

	[LoggerMessage(EventId = 3580, Level = LogLevel.Information, Message = "JIT access expiry check service started.")]
	private static partial void LogJitExpiryCheckStarted(ILogger logger);

	[LoggerMessage(EventId = 3581, Level = LogLevel.Information, Message = "JIT grant expired and revoked for user '{UserId}', scope '{GrantScope}'.")]
	private static partial void LogJitGrantExpired(ILogger logger, string userId, string grantScope);

	[LoggerMessage(EventId = 3582, Level = LogLevel.Information, Message = "JIT expiry check completed. {RevokedCount} grant(s) revoked.")]
	private static partial void LogJitExpiryCheckCompleted(ILogger logger, int revokedCount);

	[LoggerMessage(EventId = 3583, Level = LogLevel.Warning, Message = "JIT access expiry check failed.")]
	private static partial void LogJitExpiryCheckFailed(ILogger logger, Exception exception);
}
