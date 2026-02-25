// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

public class LargeOrder
{
	public string OrderId { get; set; } = string.Empty;
	public string CustomerId { get; set; } = string.Empty;
	public Collection<OrderItem> Items { get; } = [];
	public Dictionary<string, object> Metadata { get; } = [];
}
