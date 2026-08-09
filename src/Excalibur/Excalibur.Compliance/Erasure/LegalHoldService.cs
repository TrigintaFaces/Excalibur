// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Implementation of <see cref="ILegalHoldService"/> for managing legal holds
/// that block GDPR erasure per Article 17(3).
/// </summary>
/// <remarks>
/// <para>
/// Legal holds support the following GDPR Article 17(3) exceptions:
/// </para>
/// <list type="bullet">
/// <item><description>Legal claims defense</description></item>
/// <item><description>Regulatory investigation</description></item>
/// <item><description>Litigation holds</description></item>
/// <item><description>Legal obligations under EU/Member State law</description></item>
/// </list>
/// </remarks>
public sealed partial class LegalHoldService : ILegalHoldService
{
	private static readonly CompositeFormat HoldNotFoundFormat =
			CompositeFormat.Parse(Resources.LegalHoldService_HoldNotFound);
	private static readonly CompositeFormat HoldAlreadyReleasedFormat =
			CompositeFormat.Parse(Resources.LegalHoldService_HoldAlreadyReleased);

	private readonly ILegalHoldStore _store;
	private readonly ILegalHoldQueryStore _queryStore;
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ILogger<LegalHoldService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldService"/> class.
	/// </summary>
	/// <param name="store">The legal hold store.</param>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="logger">The logger.</param>
	public LegalHoldService(
		ILegalHoldStore store,
		IDataSubjectHasher dataSubjectHasher,
		ILogger<LegalHoldService> logger)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_queryStore = (ILegalHoldQueryStore?)store.GetService(typeof(ILegalHoldQueryStore))
			?? throw new InvalidOperationException("The legal hold store does not support query operations.");
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<LegalHold> CreateHoldAsync(
		LegalHoldRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateRequest(request);

		var holdId = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;

		// Hash the data subject ID if provided
		string? dataSubjectIdHash = null;
		if (!string.IsNullOrEmpty(request.DataSubjectId))
		{
			dataSubjectIdHash = HashDataSubjectId(request.DataSubjectId);
		}

		var hold = new LegalHold
		{
			HoldId = holdId,
			DataSubjectIdHash = dataSubjectIdHash,
			IdType = request.IdType,
			TenantId = request.TenantId,
			Basis = request.Basis,
			CaseReference = request.CaseReference,
			Description = request.Description,
			IsActive = true,
			ExpiresAt = request.ExpiresAt,
			CreatedBy = request.CreatedBy,
			CreatedAt = now
		};

		await _store.SaveHoldAsync(hold, cancellationToken).ConfigureAwait(false);

		LogLegalHoldCreated(holdId, request.Basis, request.CaseReference);

		return hold;
	}

	/// <inheritdoc />
	public async Task ReleaseHoldAsync(
		Guid holdId,
		string reason,
		string releasedBy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		ArgumentException.ThrowIfNullOrWhiteSpace(releasedBy);

		var hold = await _store.GetHoldAsync(holdId, cancellationToken).ConfigureAwait(false)
				?? throw new KeyNotFoundException(string.Format(
						CultureInfo.CurrentCulture,
						HoldNotFoundFormat,
						holdId));

		if (!hold.IsActive)
		{
			throw new InvalidOperationException(string.Format(
					CultureInfo.CurrentCulture,
					HoldAlreadyReleasedFormat,
					holdId));
		}

		var releasedHold = hold with
		{
			IsActive = false,
			ReleasedBy = releasedBy,
			ReleasedAt = DateTimeOffset.UtcNow,
			ReleaseReason = reason
		};

		_ = await _store.UpdateHoldAsync(releasedHold, cancellationToken).ConfigureAwait(false);

		LogLegalHoldReleased(holdId, releasedBy, reason);
	}

	/// <inheritdoc />
	public async Task<LegalHoldCheckResult> CheckHoldsAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

		var dataSubjectIdHash = HashDataSubjectId(dataSubjectId);
		var activeHolds = new List<LegalHold>();

		// Check for data subject-specific holds
		var subjectHolds = await _queryStore.GetActiveHoldsForDataSubjectAsync(
			dataSubjectIdHash,
			tenantId,
			cancellationToken).ConfigureAwait(false);
		activeHolds.AddRange(subjectHolds);

		// Tenant-wide holds. A MISSING TENANT MUST NOT MEAN "NO HOLDS".
		//
		// This block used to be skipped entirely when no tenant was supplied, and the effect
		// was the opposite of safe. A tenant-wide hold carries no data-subject identifier, and
		// the subject query above matches on that identifier -- a null never equals a value --
		// so the subject path can never return one either. With no tenant, the check therefore
		// saw ZERO tenant-wide holds, reported nothing blocking, and the erasure proceeded.
		//
		// Erasure is irreversible, and a legal hold exists precisely to prevent it, so the
		// failure was silent and unrecoverable in the direction that matters.
		//
		// When no tenant is supplied we now consult EVERY active hold instead of none. The
		// data-subject filter below still restricts the result to genuinely tenant-wide holds,
		// so the widening cannot pull in another subject's hold. The consequences are safe in
		// both deployments:
		//
		//   single-tenant   holds are stored with no tenant, so they are found -- previously
		//                   they were missed, which is the bug this fixes
		//   multi-tenant    a caller who omits the tenant sees tenant-wide holds across
		//                   tenants, so erasure is over-blocked rather than wrongly permitted
		//
		// Over-blocking is recoverable: release the hold, or pass the tenant. Under-blocking
		// destroys data that a court order said to keep.
		var tenantHolds = string.IsNullOrEmpty(tenantId)
			? await _queryStore.ListActiveHoldsAsync(null, cancellationToken).ConfigureAwait(false)
			: await _queryStore.GetActiveHoldsForTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

		// Tenant-wide holds only -- those without a specific data subject.
		var widened = 0;
		foreach (var hold in tenantHolds)
		{
			if (hold.DataSubjectIdHash is null && !activeHolds.Any(h => h.HoldId == hold.HoldId))
			{
				activeHolds.Add(hold);
				widened++;
			}
		}

		// SAY SO WHEN THE WIDENING ACTUALLY BROADENS THE ANSWER.
		//
		// Blocking more than the caller asked for is the safe direction, but doing it silently
		// is not: an operator who meant to scope the check to one tenant and omitted the
		// argument would see an erasure refused with nothing to explain why. So the widening
		// is reported at Warning, and only when it CHANGED the outcome -- a single-tenant
		// deployment, where no tenant is the normal case and this path is simply correct,
		// stays quiet.
		if (widened > 0 && string.IsNullOrEmpty(tenantId))
		{
			LogLegalHoldCheckedWithoutTenant(widened);
		}

		if (activeHolds.Count == 0)
		{
			return LegalHoldCheckResult.NoHolds;
		}

		var holdInfos = activeHolds.Select(h => new LegalHoldInfo
		{
			HoldId = h.HoldId,
			Basis = h.Basis,
			CaseReference = h.CaseReference,
			CreatedAt = h.CreatedAt,
			ExpiresAt = h.ExpiresAt
		}).ToList();

		LogLegalHoldCheckCompleted(activeHolds.Count, idType);

		return LegalHoldCheckResult.WithHolds(holdInfos);
	}

	/// <inheritdoc />
	public async Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		return await _store.GetHoldAsync(holdId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		return await _queryStore.ListActiveHoldsAsync(tenantId, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Processes expired holds by auto-releasing them.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of holds released.</returns>
	internal async Task<int> ProcessExpiredHoldsAsync(CancellationToken cancellationToken)
	{
		var expiredHolds = await _queryStore.GetExpiredHoldsAsync(cancellationToken).ConfigureAwait(false);

		var releasedCount = 0;
		List<Guid>? failedHoldIds = null;
		foreach (var hold in expiredHolds)
		{
			try
			{
				await ReleaseHoldAsync(
					hold.HoldId,
					"Auto-released due to expiration",
					"System",
					cancellationToken).ConfigureAwait(false);
				releasedCount++;
			}
			catch (Exception ex)
			{
				LogLegalHoldAutoReleaseFailed(hold.HoldId, ex);
				failedHoldIds ??= [];
				failedHoldIds.Add(hold.HoldId);
			}
		}

		if (releasedCount > 0)
		{
			LogLegalHoldAutoReleaseCompleted(releasedCount);
		}

		if (failedHoldIds is { Count: > 0 })
		{
			LogLegalHoldAutoReleasePartialFailure(failedHoldIds.Count, string.Join(", ", failedHoldIds));
		}

		return releasedCount;
	}

	private static void ValidateRequest(LegalHoldRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.DataSubjectId) && string.IsNullOrWhiteSpace(request.TenantId))
		{
			throw new ArgumentException(Resources.LegalHoldService_HoldSubjectOrTenantRequired);
		}

		if (string.IsNullOrWhiteSpace(request.CaseReference))
		{
			throw new ArgumentException(Resources.LegalHoldService_CaseReferenceRequired, nameof(request));
		}

		if (string.IsNullOrWhiteSpace(request.Description))
		{
			throw new ArgumentException(Resources.LegalHoldService_DescriptionRequired, nameof(request));
		}

		if (string.IsNullOrWhiteSpace(request.CreatedBy))
		{
			throw new ArgumentException(Resources.LegalHoldService_CreatedByRequired, nameof(request));
		}

		if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
		{
			throw new ArgumentException(Resources.LegalHoldService_ExpiresAtMustBeFuture, nameof(request));
		}
	}

	private string HashDataSubjectId(string dataSubjectId) =>
		_dataSubjectHasher.HashDataSubjectId(dataSubjectId);

	[LoggerMessage(
			ComplianceEventId.LegalHoldCreated,
			LogLevel.Information,
			"Created legal hold {HoldId} for basis {Basis}, case {CaseReference}")]
	private partial void LogLegalHoldCreated(Guid holdId, LegalHoldBasis basis, string caseReference);

	[LoggerMessage(
			ComplianceEventId.LegalHoldReleased,
			LogLevel.Information,
			"Released legal hold {HoldId} by {ReleasedBy}. Reason: {Reason}")]
	private partial void LogLegalHoldReleased(Guid holdId, string releasedBy, string reason);

	[LoggerMessage(
			ComplianceEventId.LegalHoldCheckedWithoutTenant,
			LogLevel.Warning,
			"Legal-hold check ran without a tenant, so all active holds were consulted; {Count} tenant-wide hold(s) applied. Pass the tenant to scope this check.")]
	private partial void LogLegalHoldCheckedWithoutTenant(int count);

	[LoggerMessage(
			ComplianceEventId.LegalHoldCheckCompleted,
			LogLevel.Debug,
			"Found {Count} active legal holds for data subject type {IdType}")]
	private partial void LogLegalHoldCheckCompleted(int count, DataSubjectIdType idType);

	[LoggerMessage(
			ComplianceEventId.LegalHoldAutoReleaseFailed,
			LogLevel.Error,
			"Failed to auto-release expired hold {HoldId}")]
	private partial void LogLegalHoldAutoReleaseFailed(Guid holdId, Exception exception);

	[LoggerMessage(
			ComplianceEventId.LegalHoldAutoReleaseCompleted,
			LogLevel.Information,
			"Auto-released {Count} expired legal holds")]
	private partial void LogLegalHoldAutoReleaseCompleted(int count);

	[LoggerMessage(
			ComplianceEventId.LegalHoldAutoReleasePartialFailure,
			LogLevel.Warning,
			"Failed to auto-release {FailedCount} expired legal holds. Hold IDs: {HoldIds}")]
	private partial void LogLegalHoldAutoReleasePartialFailure(int failedCount, string holdIds);
}
