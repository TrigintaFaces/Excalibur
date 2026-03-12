// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace DispatchMinimal.Messages;

/// <summary>
/// An event indicating an order was created.
/// Events represent facts that have occurred - they can have multiple handlers.
/// </summary>
public record OrderCreatedEvent(Guid OrderId, string ProductId, int Quantity) : IDispatchEvent;
