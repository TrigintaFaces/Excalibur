// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Dispatch;
using Excalibur.Saga.Oracle.Requests;
using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Tests.Oracle;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Conformance")]
public sealed class OraclePurgePredicateConformanceShould
{
    [Theory]
    [InlineData("tenant-a")]
    [InlineData("tenant-b")]
    [InlineData(null)]
    public void BindOnlyTheRequestedPartition(string? tenant)
    {
        var scope = tenant is null ? TenantScope.Untenanted : TenantScope.Scoped(tenant);
        var request = PurgeCompletedSagasRequest.ForTenant(DateTimeOffset.UnixEpoch, "APP.SAGAS", scope, CancellationToken.None);
        request.Command.CommandText.ShouldContain("AND TenantId = :TenantId");
        using var connection = new OracleConnection();
        using var command = connection.CreateCommand();
        const string expectedSql = "DELETE FROM APP.SAGAS WHERE CompletedAt IS NOT NULL AND CompletedAt < :Threshold AND TenantId = :TenantId";
        request.Command.CommandText.ShouldBe(expectedSql);
        command.CommandText = expectedSql;
        new MaterializeParameters((SqlMapper.IDynamicParameters)request.Command.Parameters!).AddParameters(command);
        command.BindByName.ShouldBeTrue();
        command.Parameters["TenantId"].Value.ShouldBe(tenant ?? KeyedTenantPartition.Untenanted.TenantId);
        command.Parameters["Threshold"].Value.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void OmitTenantOnlyForTheNamedEstateWideOperation()
    {
        var request = PurgeCompletedSagasRequest.ForAllTenants(DateTimeOffset.UnixEpoch, "APP.SAGAS", CancellationToken.None);
        request.Command.CommandText.ShouldContain("DELETE FROM");
        request.Command.CommandText.ShouldNotContain(":TenantId");
        using var connection = new OracleConnection();
        using var command = connection.CreateCommand();
        const string expectedSql = "DELETE FROM APP.SAGAS WHERE CompletedAt IS NOT NULL AND CompletedAt < :Threshold";
        request.Command.CommandText.ShouldBe(expectedSql);
        command.CommandText = expectedSql;
        new MaterializeParameters((SqlMapper.IDynamicParameters)request.Command.Parameters!).AddParameters(command);
        command.Parameters.Contains("TenantId").ShouldBeFalse();
        command.Parameters.Count.ShouldBe(1);
    }

    private sealed class MaterializeParameters(SqlMapper.IDynamicParameters inner) : DynamicParameters, SqlMapper.IDynamicParameters
    {
        void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity) => inner.AddParameters(command, identity);
    }
}
