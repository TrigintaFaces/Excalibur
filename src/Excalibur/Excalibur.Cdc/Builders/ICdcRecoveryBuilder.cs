// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Fluent builder interface for configuring CDC recovery settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures how the CDC processor handles scenarios where the saved
/// position (LSN) is no longer valid in the database. This can occur due to CDC
/// cleanup jobs, database restores, or CDC being disabled and re-enabled.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.WithRecovery(recovery =>
/// {
///     recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
///             .MaxAttempts(5)
///             .AttemptDelay(TimeSpan.FromSeconds(30))
///             .OnPositionReset((args, ct) =&gt; { /* custom handling */ })
///             .EnableStructuredLogging(true);
/// });
/// </code>
/// </example>
public interface ICdcRecoveryBuilder
{
	/// <summary>
	/// Sets the recovery strategy for stale positions.
	/// </summary>
	/// <param name="strategy">The recovery strategy to use.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Available strategies:
	/// <list type="bullet">
	/// <item><description><c>Throw</c> - Throws exception (legacy behavior)</description></item>
	/// <item><description><c>FallbackToEarliest</c> - Resume from earliest (safe, may reprocess)</description></item>
	/// <item><description><c>FallbackToLatest</c> - Skip to latest (data loss possible)</description></item>
	/// <item><description><c>InvokeCallback</c> - Custom handling via callback</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Default is <c>FallbackToEarliest</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// recovery.Strategy(StalePositionRecoveryStrategy.FallbackToLatest);
	/// </code>
	/// </example>
	ICdcRecoveryBuilder Strategy(StalePositionRecoveryStrategy strategy);

	/// <summary>
	/// Sets the maximum number of recovery attempts before giving up.
	/// </summary>
	/// <param name="count">The maximum number of retry attempts.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// After exhausting all recovery attempts, the processor will throw
	/// a <c>CdcStalePositionException</c> regardless of the configured strategy.
	/// </para>
	/// <para>
	/// Default is 3 attempts.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// recovery.MaxAttempts(5);
	/// </code>
	/// </example>
	ICdcRecoveryBuilder MaxAttempts(int count);

	/// <summary>
	/// Sets the delay between recovery attempts.
	/// </summary>
	/// <param name="delay">The delay between attempts.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="delay"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 1 second.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// recovery.AttemptDelay(TimeSpan.FromSeconds(30));
	/// </code>
	/// </example>
	ICdcRecoveryBuilder AttemptDelay(TimeSpan delay);

	/// <summary>
	/// Configures a custom handler for position reset events.
	/// </summary>
	/// <param name="handler">The handler delegate.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="handler"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This callback is required when <c>Strategy</c> is set to <c>InvokeCallback</c>.
	/// The callback is also invoked (if configured) for other strategies to allow
	/// logging or alerting, but the recovery action is determined by the strategy.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// recovery.OnPositionReset(async (args, ct) =>
	/// {
	///     logger.LogWarning("Position reset for {CaptureInstance}", args.CaptureInstance);
	///     await Task.CompletedTask;
	/// });
	/// </code>
	/// </example>
	ICdcRecoveryBuilder OnPositionReset(CdcPositionResetHandler handler);

	/// <summary>
	/// Enables or disables structured logging for recovery events.
	/// </summary>
	/// <param name="enable">Whether to enable structured logging. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, detailed structured log events will be emitted for position
	/// resets and recovery attempts, useful for monitoring and alerting.
	/// </para>
	/// <para>
	/// Default is enabled.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// recovery.EnableStructuredLogging(false);
	/// </code>
	/// </example>
	ICdcRecoveryBuilder EnableStructuredLogging(bool enable = true);
}
