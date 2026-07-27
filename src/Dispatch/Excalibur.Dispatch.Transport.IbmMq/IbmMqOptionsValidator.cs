// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.IbmMq;

/// <summary>
/// Validates <see cref="IbmMqOptions"/> at startup (fail-fast) so a misconfigured IBM MQ transport is
/// rejected before the first send/receive rather than surfacing as a runtime connection failure.
/// </summary>
internal sealed class IbmMqOptionsValidator : IValidateOptions<IbmMqOptions>
{
	public ValidateOptionsResult Validate(string? name, IbmMqOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.QueueManager))
		{
			failures.Add($"{nameof(IbmMqOptions.QueueManager)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Host))
		{
			failures.Add($"{nameof(IbmMqOptions.Host)} is required.");
		}

		if (options.Port is < 1 or > 65535)
		{
			failures.Add($"{nameof(IbmMqOptions.Port)} must be in the range 1..65535.");
		}

		if (string.IsNullOrWhiteSpace(options.Channel))
		{
			failures.Add($"{nameof(IbmMqOptions.Channel)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.QueueName))
		{
			failures.Add($"{nameof(IbmMqOptions.QueueName)} is required.");
		}

		// The receiver opens one queue-manager connection per in-flight message (unit-of-work-per-message),
		// so MaxBatchSize is the hard bound on concurrent connections — cap it to protect the queue manager's
		// connection pool from an accidental large value.
		if (options.Receive.MaxBatchSize is < 1 or > IbmMqReceiveTuningOptions.MaxBatchSizeCeiling)
		{
			failures.Add($"{nameof(IbmMqOptions.Receive)}.{nameof(IbmMqReceiveTuningOptions.MaxBatchSize)} must be in the range 1..{IbmMqReceiveTuningOptions.MaxBatchSizeCeiling}.");
		}

		if (options.Receive.MaxOutstandingUnitsOfWork < 1)
		{
			failures.Add($"{nameof(IbmMqOptions.Receive)}.{nameof(IbmMqReceiveTuningOptions.MaxOutstandingUnitsOfWork)} must be at least 1.");
		}

		if (options.Receive.WaitIntervalMilliseconds < 0)
		{
			failures.Add($"{nameof(IbmMqOptions.Receive)}.{nameof(IbmMqReceiveTuningOptions.WaitIntervalMilliseconds)} must be non-negative.");
		}

		if (options.Receive.MaxPayloadBytes is < 1)
		{
			failures.Add($"{nameof(IbmMqOptions.Receive)}.{nameof(IbmMqReceiveTuningOptions.MaxPayloadBytes)} must be at least 1 byte when specified. Set it to null to opt out of the payload-size limit.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
