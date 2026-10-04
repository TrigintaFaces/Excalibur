// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using DispatchMinimal.Messages;

namespace DispatchMinimal;

/// <summary>
/// In-memory state for this single-process demonstration. A production application would persist orders.
/// </summary>
public sealed class OrderStore
{
	public ConcurrentDictionary<Guid, OrderDto> Orders { get; } = new();

	public ConcurrentDictionary<Guid, OrderDto> ReadModel { get; } = new();

	public ConcurrentDictionary<Guid, byte> Notifications { get; } = new();

	public ConcurrentDictionary<Guid, OrderDto> Documents { get; } = new();
}
