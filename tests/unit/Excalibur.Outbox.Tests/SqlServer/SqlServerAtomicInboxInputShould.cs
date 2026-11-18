// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerAtomicInboxInputShould
{
    private const string ConnectionString = "Server=unused;Database=Messaging;Integrated Security=true;TrustServerCertificate=true";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task RejectInvalidInputsBeforeCallingTheConnectionFactory(int invalid)
    {
        var factoryCalls = 0;
        var store = new SqlServerOutboxStore(() =>
        {
            factoryCalls++;
            throw new InvalidOperationException("Connection factory must not run for invalid inputs");
        }, new SqlServerOutboxOptions { ConnectionString = ConnectionString }, payloadSerializer: null,
            new SqlServerInboxOptions { ConnectionString = ConnectionString }, NullLogger<SqlServerOutboxStore>.Instance);
        string outgoing = "outgoing";
        InboxEntry? entry = Entry();
        switch (invalid)
        {
            case 0: outgoing = null!; break;
            case 1: outgoing = " "; break;
            case 2: entry = null; break;
            case 3: entry.MessageId = null!; break;
            case 4: entry.MessageId = " "; break;
            case 5: entry.HandlerType = " "; break;
            case 6: entry.MessageType = " "; break;
            case 7: entry.Payload = null!; break;
            case 8: entry = new InboxEntry("incoming", "handler", "type", [1]) { Metadata = null! }; break;
        }
        _ = await Should.ThrowAsync<ArgumentException>(async () =>
            await store.TryMarkSentAndReceivedAsync(outgoing, entry!, CancellationToken.None).ConfigureAwait(false));
        factoryCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnFalseForNoInboxOrDifferentDatabaseWithoutCallingTheConnectionFactory(bool differentDatabase)
    {
        var factoryCalls = 0;
        SqlConnection Factory()
        {
            factoryCalls++;
            throw new InvalidOperationException("Unavailable transactional completion must perform no database I/O");
        }
        var store = new SqlServerOutboxStore(Factory,
            new SqlServerOutboxOptions { ConnectionString = ConnectionString }, payloadSerializer: null,
            differentDatabase ? new SqlServerInboxOptions { ConnectionString = ConnectionString.Replace("Database=Messaging", "Database=Other", StringComparison.Ordinal) } : null,
            NullLogger<SqlServerOutboxStore>.Instance);
        (await store.TryMarkSentAndReceivedAsync("outgoing", Entry(), CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
        factoryCalls.ShouldBe(0);
    }

    private static InboxEntry Entry() => new("incoming", "handler", "type", [1]);
}
