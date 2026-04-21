// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace MultiTenantEventSourcing.Commands;

/// <summary>Request body for <c>POST /orders</c> in the multi-tenant sample.</summary>
public sealed record CreateOrderRequest(decimal Total);
