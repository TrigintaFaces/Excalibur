// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Base class for message bus configuration options, providing common settings for message bus instances.
/// </summary>
public abstract class IMessageBusOptions
{
	/// <summary>
	/// Gets the name of the message bus instance.
	/// </summary>
	/// <value> The logical name of the bus configuration. </value>
	public string Name { get; init; } = null!;

	/// <summary>
	/// Gets a value indicating whether messages sent through this bus should be encrypted.
	/// </summary>
	/// <value> <see langword="true" /> when encryption should be applied; otherwise, <see langword="false" />. </value>
	public bool EnableEncryption { get; init; }

	/// <summary>
	/// Gets the key identifier used to select the encryption provider.
	/// </summary>
	/// <value> The key identifying the encryption provider. </value>
	public string? EncryptionProviderKey { get; init; }

	/// <summary>
	/// Gets a value indicating whether messages sent through this bus should be retried.
	/// </summary>
	/// <value> <see langword="true" /> when retry should be attempted; otherwise, <see langword="false" />. </value>
	public bool EnableRetries { get; init; }

	/// <summary>
	/// Gets the maximum retry attempts.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	public int MaxRetryAttempts { get; init; } = 3;

	/// <summary>
	/// Gets the strategy used to calculate delays between retries.
	/// </summary>
	/// <value> The retry backoff strategy. </value>
	public RetryStrategy RetryStrategy { get; init; } = RetryStrategy.FixedDelay;

	/// <summary>
	/// Gets the delay between retry attempts.
	/// </summary>
	/// <value> The base delay interval between retries. </value>
	public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Gets the percentage of randomness applied to the calculated delay. Use values between 0 and 1. Set to 0 to disable jitter.
	/// </summary>
	/// <value> The jitter factor applied to retry scheduling. </value>
	public double JitterFactor { get; init; }

	/// <summary>
	/// Gets the optional URI for remote dispatch (used in forwarding or remote buses).
	/// </summary>
	/// <value> The remote target URI for dispatch. </value>
	public Uri? TargetUri { get; init; }

	/// <summary>
	/// Gets a value indicating whether tracing is enabled for this bus with OpenTelemetry spans.
	/// </summary>
	/// <value> <see langword="true" /> to enable telemetry emission; otherwise, <see langword="false" />. </value>
	public bool EnableTelemetry { get; init; } = true;
}
