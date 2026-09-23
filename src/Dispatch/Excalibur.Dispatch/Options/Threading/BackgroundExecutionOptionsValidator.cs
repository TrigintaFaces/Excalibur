// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Threading;

/// <summary>
/// Validates <see cref="BackgroundExecutionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// <see cref="BackgroundExecutionExceptionBehavior.StopHost"/> can only be honoured through
/// <see cref="IHostApplicationLifetime"/>. Without one, a failing background message could not stop anything, so
/// the configuration is refused at startup rather than silently degrading to log-only at the first failure.
/// </remarks>
/// <param name="hostApplicationLifetime">The host's lifetime, when the application runs under a host.</param>
internal sealed class BackgroundExecutionOptionsValidator(IHostApplicationLifetime? hostApplicationLifetime = null)
	: IValidateOptions<BackgroundExecutionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, BackgroundExecutionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!Enum.IsDefined(options.ExceptionBehavior))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(BackgroundExecutionOptions)}.{nameof(BackgroundExecutionOptions.ExceptionBehavior)} has the " +
				$"undefined value {(int)options.ExceptionBehavior}. Use {nameof(BackgroundExecutionExceptionBehavior.LogOnly)} " +
				$"or {nameof(BackgroundExecutionExceptionBehavior.StopHost)}.");
		}

		if (options.ExceptionBehavior == BackgroundExecutionExceptionBehavior.StopHost && hostApplicationLifetime is null)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(BackgroundExecutionOptions)}.{nameof(BackgroundExecutionOptions.ExceptionBehavior)} is " +
				$"{nameof(BackgroundExecutionExceptionBehavior.StopHost)}, but no {nameof(IHostApplicationLifetime)} is " +
				"registered, so a failing background message could not stop the host. Run the application under a " +
				$"generic host, or use {nameof(BackgroundExecutionExceptionBehavior.LogOnly)}.");
		}

		return ValidateOptionsResult.Success;
	}
}
