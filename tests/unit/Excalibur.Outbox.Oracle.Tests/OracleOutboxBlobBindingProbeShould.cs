// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

public sealed partial class OracleOutboxEnqueueTransformShould
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task PreserveKnownPayloadParameterReplacementAtExecution(bool scheduled, bool replaceWithEmpty) =>
        AssertKnownPayloadReplacementAsync(scheduled, replaceWithEmpty, useDapper: false);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task PreserveKnownPayloadReplacementThroughTheDeclaredDapperCommand(bool scheduled, bool replaceWithEmpty) =>
        AssertKnownPayloadReplacementAsync(scheduled, replaceWithEmpty, useDapper: true);

    private async Task AssertKnownPayloadReplacementAsync(bool scheduled, bool replaceWithEmpty, bool useDapper)
    {
        await RunOwnedBlobAsync(async (unusedHarness, id, token) =>
        {
            byte[] original = replaceWithEmpty ? [7, 8] : [];
            byte[] expected = replaceWithEmpty ? [] : [0, 255, 37];
            await using var connection = fixture.CreateConnection(); await connection.OpenAsync(token);
            if (scheduled)
            {
                var request = new ScheduleOutboxMessage(id, "binary", "{}", original, "before-tenant", "binary-destination",
                    DateTimeOffset.UtcNow.AddHours(1), "OUTBOX", 30, token);
                var parameters = request.Command.Parameters.ShouldBeAssignableTo<DynamicParameters>().ShouldNotBeNull();
                parameters.ParameterNames.ShouldContain("MessageBody", "the existing known replacement parameter remains available regardless of its initial length");
                parameters.Add("MessageBody", expected, direction: ParameterDirection.Input);
                if (useDapper) { (await connection.ExecuteAsync(request.Command)).ShouldBe(1); }
                else { (await request.ResolveAsync(connection)).ShouldBe(1); }
            }
            else
            {
                var request = new InsertOutboxMessage(id, "binary", "{}", original, DateTimeOffset.UtcNow,
                    "before-tenant", "binary-destination", null, null, 0, null, null, null, 0, null, false, "OUTBOX", 30, token);
                var parameters = request.Command.Parameters.ShouldBeAssignableTo<DynamicParameters>().ShouldNotBeNull();
                parameters.ParameterNames.ShouldContain("MessageBody", "the existing known replacement parameter remains available regardless of its initial length");
                parameters.Add("MessageBody", expected, direction: ParameterDirection.Input);
                if (useDapper) { (await connection.ExecuteAsync(request.Command)).ShouldBe(1); }
                else { (await request.ResolveAsync(connection)).ShouldBe(1); }
            }
            AssertBlob((await ReadOwnedBlobAsync(id, false, token)).Single(), expected);
        });
    }
}
