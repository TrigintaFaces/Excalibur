// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of an outbox publishing operation.
/// </summary>
public sealed class PublishingResult
{
	/// <summary>
	/// Gets the number of messages successfully published.
	/// </summary>
	/// <value> The current <see cref="SuccessCount" /> value. </value>
	public int SuccessCount { get; init; }

	/// <summary>
	/// Gets the number of messages that failed to publish.
	/// </summary>
	/// <value> The current <see cref="FailureCount" /> value. </value>
	public int FailureCount { get; init; }

	/// <summary>
	/// Gets the number of messages that were skipped (e.g., not ready for delivery).
	/// </summary>
	/// <value> The current <see cref="SkippedCount" /> value. </value>
	public int SkippedCount { get; init; }

	/// <summary>
	/// Gets the total number of messages processed.
	/// </summary>
	/// <value> The current <see cref="TotalProcessed" /> value. </value>
	public int TotalProcessed => SuccessCount + FailureCount + SkippedCount;

	/// <summary>
	/// Gets the success rate as a percentage.
	/// </summary>
	/// <value> The current <see cref="SuccessRate" /> value. </value>
	public double SuccessRate => TotalProcessed > 0 ? SuccessCount * 100.0 / TotalProcessed : 100.0;

	/// <summary>
	/// Gets the duration of the publishing operation.
	/// </summary>
	/// <value> The current <see cref="Duration" /> value. </value>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets any errors that occurred during publishing.
	/// </summary>
	public IReadOnlyList<PublishingError> Errors { get; init; } = Array.Empty<PublishingError>();

	/// <summary>
	/// Gets the timestamp when this result was created.
	/// </summary>
	/// <value> The current <see cref="Timestamp" /> value. </value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets a value indicating whether the operation was successful.
	/// </summary>
	/// <value> The current <see cref="IsSuccess" /> value. </value>
	public bool IsSuccess => FailureCount == 0;

	/// <summary>
	/// Creates a successful publishing result.
	/// </summary>
	/// <param name="successCount"> Number of messages successfully published. </param>
	/// <param name="skippedCount"> Number of messages skipped. </param>
	/// <param name="duration"> Duration of the operation. </param>
	/// <returns> A successful publishing result. </returns>
	public static PublishingResult Success(int successCount, int skippedCount = 0, TimeSpan duration = default) =>
		new() { SuccessCount = successCount, SkippedCount = skippedCount, Duration = duration };

	/// <summary>
	/// Creates a failed publishing result.
	/// </summary>
	/// <param name="successCount"> Number of messages successfully published. </param>
	/// <param name="failureCount"> Number of messages that failed to publish. </param>
	/// <param name="errors"> Errors that occurred during publishing. </param>
	/// <param name="duration"> Duration of the operation. </param>
	/// <returns> A failed publishing result. </returns>
	public static PublishingResult WithFailures(
		int successCount,
		int failureCount,
		IEnumerable<PublishingError> errors,
		TimeSpan duration = default) =>
		new() { SuccessCount = successCount, FailureCount = failureCount, Errors = errors.ToList(), Duration = duration };

	/// <inheritdoc />
	public override string ToString() =>
		$"PublishingResult: {SuccessCount} success, {FailureCount} failed, {SkippedCount} skipped in {Duration.TotalMilliseconds:F0}ms";
}
