// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Specifies the delivery guarantee semantics for message processing.
/// </summary>
public enum DeliveryGuarantee
{
	/// <summary>
	/// At-most-once delivery: Messages may be lost but will never be delivered more than once.
	/// The message is marked as processed before the handler is invoked.
	/// Best for high-throughput scenarios where occasional message loss is acceptable.
	/// </summary>
	AtMostOnce = 0,

	/// <summary>
	/// At-least-once delivery: Messages will be delivered at least once but may be duplicated.
	/// The message is marked as processed only after the handler succeeds.
	/// Requires idempotent handlers to handle potential duplicates.
	/// This is the default and recommended setting for most scenarios.
	/// </summary>
	AtLeastOnce = 1,
}

/// <summary>
/// Configuration options for delivery guarantee behavior.
/// </summary>
public sealed class DeliveryGuaranteeOptions
{
	/// <summary>
	/// Gets or sets the delivery guarantee semantics. Default is <see cref="DeliveryGuarantee.AtLeastOnce"/>.
	/// </summary>
	public DeliveryGuarantee Guarantee { get; set; } = DeliveryGuarantee.AtLeastOnce;

	/// <summary>
	/// Gets or sets a value indicating whether to enable idempotency tracking for at-least-once delivery.
	/// When enabled, the system will track processed message IDs to detect and skip duplicates.
	/// </summary>
	public bool EnableIdempotencyTracking { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration to retain idempotency keys for duplicate detection.
	/// Default is 7 days.
	/// </summary>
	public TimeSpan IdempotencyKeyRetention { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets a value indicating whether to automatically retry failed deliveries.
	/// Only applies to at-least-once delivery guarantee.
	/// </summary>
	public bool EnableAutomaticRetry { get; set; } = true;
}
