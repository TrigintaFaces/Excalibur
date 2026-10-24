// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Postgres;

namespace Excalibur.Data.Postgres.Tests.Postgres.Requests.Saga;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Conformance")]
public sealed class PurgePredicateConformanceShould
{
    [Theory]
    [InlineData("tenant-a")]
    [InlineData("tenant-b")]
    [InlineData(null)]
    public void BindOnlyTheRequestedPartition(string? tenant)
    {
        var scope = tenant is null ? TenantScope.Untenanted : TenantScope.Scoped(tenant);
        var request = PurgeCompletedSagasRequest.ForTenant(DateTimeOffset.UnixEpoch, new PostgresSagaOptions(), scope, CancellationToken.None);
        request.Command.CommandText.ShouldContain("AND tenant_id = @TenantId");
        request.Parameters.Get<string>("TenantId").ShouldBe(tenant ?? KeyedTenantPartition.Untenanted.TenantId);
        request.Parameters.Get<DateTimeOffset>("Threshold").ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void OmitTenantOnlyForTheNamedEstateWideOperation()
    {
        var request = PurgeCompletedSagasRequest.ForAllTenants(DateTimeOffset.UnixEpoch, new PostgresSagaOptions(), CancellationToken.None);
        request.Command.CommandText.ShouldContain("DELETE FROM");
        request.Command.CommandText.ShouldNotContain("@TenantId");
        request.Parameters.ParameterNames.ShouldNotContain("TenantId");
        request.Parameters.ParameterNames.ShouldHaveSingleItem().ShouldBe("Threshold");
    }
}
