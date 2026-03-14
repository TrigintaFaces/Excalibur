// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace DispatchMinimal.Messages;

/// <summary>
/// A command to create a new order.
/// Commands represent intent to change state and return the new order ID.
/// </summary>
public record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction<Guid>;
