// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetrySample.Models;

/// <summary>
/// Order request model.
/// </summary>
public record OrderRequest(string OrderId, string CustomerId, decimal Amount);
