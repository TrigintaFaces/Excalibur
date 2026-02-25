// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace MessageEncryptionSample.Messages;

/// <summary>
/// Event representing a processed payment with sensitive data.
/// This demonstrates encryption of sensitive fields like credit card numbers.
/// </summary>
public sealed record PaymentProcessedEvent(
	string PaymentId,
	string CustomerId,
	decimal Amount,
	string Currency,
	string MaskedCardNumber,
	string EncryptedCardData,
	DateTimeOffset ProcessedAt) : IDispatchEvent;
