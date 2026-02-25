// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace OpenTelemetrySample.Messages;

/// <summary>
/// Event raised when an order is processed.
/// </summary>
public sealed record OrderProcessedEvent(
	string OrderId,
	string CustomerId,
	decimal Amount,
	DateTimeOffset ProcessedAt) : IDispatchEvent;
