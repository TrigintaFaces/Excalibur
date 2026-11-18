// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests;

/// <summary>Read-only test observation of fields not exposed by the public terminal-row views.</summary>
internal static class PersistedInMemoryOutboxState
{
    // Never return the stored envelope or payload. Call only after the drain's owned work has joined.
    internal static (OutboxStatus Status, int RetryCount, string? LastError, byte[] Payload, string? TenantId)
        Read(ServiceProvider provider, string messageId)
    {
        var store = provider.GetRequiredService<InMemoryOutboxStore>();
        var field = typeof(InMemoryOutboxStore).GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull();
        var messages = (IReadOnlyDictionary<string, OutboundMessage>)field.GetValue(store)!;
        var stored = messages[messageId];
        return (stored.Status, stored.RetryCount, stored.LastError, (byte[])stored.Payload.Clone(), stored.TenantId);
    }
}
