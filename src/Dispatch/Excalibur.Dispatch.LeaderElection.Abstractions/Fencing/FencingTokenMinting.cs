// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Mints a fencing token for a leadership tenure, retrying only the failures that retrying can fix.
/// </summary>
/// <remarks>
/// A tenure must not be declared without a valid token, so every provider mints one at the moment it
/// takes the lock and relinquishes leadership if it cannot. That made this loop provider-agnostic, and it
/// was duplicated into each provider instead — where the copies drifted apart on which failures are worth
/// retrying. One implementation, so the retry policy for a guarantee-critical step has a single home.
/// </remarks>
public static class FencingTokenMinting
{
	/// <summary>
	/// The number of mint attempts before leadership is relinquished.
	/// </summary>
	/// <remarks>
	/// Deliberately small and without a delay between attempts. Every attempt spends part of the grace
	/// period during which the node still believes it may lead, so a slow retry schedule here does not
	/// buy reliability — it widens the window in which a node acts on an un-advanced fence.
	/// </remarks>
	public const int DefaultMaxAttempts = 3;

	/// <summary>
	/// Mints a fencing token, retrying transient failures up to <paramref name="maxAttempts"/> times.
	/// </summary>
	/// <param name="provider"> The provider that issues tokens for this resource. </param>
	/// <param name="resourceId"> The resource the leadership tenure covers. </param>
	/// <param name="cancellationToken"> Cancels the mint. </param>
	/// <param name="maxAttempts"> Attempts before giving up; defaults to <see cref="DefaultMaxAttempts"/>. </param>
	/// <returns> The minted token. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="provider"/> or <paramref name="resourceId"/> is <see langword="null"/>. </exception>
	/// <exception cref="FencingTokenExhaustedException">
	/// The token domain has no values left. Raised on the first attempt and never retried: it is permanent,
	/// so retrying returns the same answer while consuming the grace period, and the caller is documented to
	/// handle this type rather than to unwrap it from another exception.
	/// </exception>
	/// <exception cref="OperationCanceledException"> <paramref name="cancellationToken"/> was cancelled. </exception>
	/// <exception cref="InvalidOperationException">
	/// Every attempt failed for a retryable reason. The last failure is the inner exception. The caller must
	/// relinquish leadership rather than act as a leader whose fence never advanced.
	/// </exception>
	public static async Task<long> MintWithRetryAsync(
		IFencingTokenProvider provider,
		string resourceId,
		CancellationToken cancellationToken,
		int maxAttempts = DefaultMaxAttempts)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ArgumentNullException.ThrowIfNull(resourceId);

		Exception? lastError = null;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				return await provider.IssueTokenAsync(resourceId, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (FencingTokenExhaustedException)
			{
				// Permanent. Retrying produces the same answer and spends grace period doing it, and the
				// caller is documented to catch this type -- burying it as an inner exception would put the
				// one recoverable signal behind a generic failure.
				throw;
			}
			catch (Exception ex)
			{
				lastError = ex;
			}
		}

		throw new InvalidOperationException(
			$"Failed to mint a fencing token for resource '{resourceId}' after {maxAttempts} attempt(s); "
			+ "relinquishing leadership rather than acting as a fenced leader with an un-advanced fence.",
			lastError);
	}
}
