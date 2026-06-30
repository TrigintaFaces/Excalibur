// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// Wires Excalibur cold-start warmup into AWS Lambda SnapStart runtime hooks, so dependency-injection
/// initialization and JIT/SDK warmup are captured in the published function snapshot rather than paid
/// on the first invocation of each restored execution environment.
/// </summary>
/// <remarks>
/// <para>
/// SnapStart is enabled on the Lambda <em>function configuration</em> (a published version with
/// SnapStart turned on); a process cannot enable it for itself. What the framework can do is register a
/// <strong>before-snapshot</strong> hook through the managed runtime's snapshot-restore API: when
/// SnapStart captures the snapshot, the registered warmup runs once during initialization, so the
/// optimized state is baked into every restored environment instead of being recomputed after restore.
/// </para>
/// <para>
/// Call <see cref="RegisterWarmup"/> from your function's construction/initialization path (for example
/// a static initializer or the Lambda handler type's constructor) so that registration — and therefore
/// the warmup — happens at snapshot time, not on first invocation. Registration is fail-open: a runtime
/// that does not support snapshot hooks logs a warning and continues; the warmup then runs on the first
/// invocation as before. SnapStart is partly outside framework control — function-level enablement,
/// network state restoration, and uniqueness concerns (for example regenerating per-environment random
/// seeds after restore) remain the consumer's responsibility.
/// </para>
/// </remarks>
public static partial class AwsLambdaSnapStartHooks
{
	/// <summary>
	/// Registers the supplied cold-start optimizer's warmup to run before AWS Lambda SnapStart captures
	/// the snapshot, moving DI, JIT, and SDK initialization off the post-restore hot path.
	/// </summary>
	/// <param name="optimizer">The cold-start optimizer whose warmup runs before the snapshot.</param>
	/// <param name="logger">The logger used to record hook registration and registration failures.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="optimizer"/> or <paramref name="logger"/> is <see langword="null"/>.
	/// </exception>
	public static void RegisterWarmup(IColdStartOptimizer optimizer, ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(optimizer);
		ArgumentNullException.ThrowIfNull(logger);

		try
		{
			Amazon.Lambda.Core.SnapshotRestore.RegisterBeforeSnapshot(async ValueTask () =>
			{
				LogSnapStartWarmupRunning(logger);
				await optimizer.WarmupAsync().ConfigureAwait(false);
			});

			LogSnapStartHookRegistered(logger);
		}
		catch (Exception ex)
		{
			// Fail-open: a runtime without snapshot-hook support must never break host initialization.
			LogSnapStartHookRegistrationFailed(logger, ex);
		}
	}

	[LoggerMessage(AwsLambdaEventId.SnapStartHookRegistered, Microsoft.Extensions.Logging.LogLevel.Information,
		"Registered AWS Lambda SnapStart before-snapshot warmup hook; cold-start warmup will run during snapshot initialization.")]
	private static partial void LogSnapStartHookRegistered(ILogger logger);

	[LoggerMessage(AwsLambdaEventId.SnapStartWarmupRunning, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Running cold-start warmup before the AWS Lambda SnapStart snapshot.")]
	private static partial void LogSnapStartWarmupRunning(ILogger logger);

	[LoggerMessage(AwsLambdaEventId.SnapStartHookRegistrationFailed, Microsoft.Extensions.Logging.LogLevel.Warning,
		"AWS Lambda SnapStart before-snapshot hook registration was skipped; the runtime may not support snapshot hooks. Cold-start warmup will run on first invocation instead.")]
	private static partial void LogSnapStartHookRegistrationFailed(ILogger logger, Exception exception);
}
