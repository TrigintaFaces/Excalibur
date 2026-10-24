using System.Data;
using Dapper;
using Excalibur.EventSourcing.SqlServer.Requests;
using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Conformance")]
public sealed class SqlServerParameterSetShould
{
    [Fact]
    public void PreserveJitSnapshotMetadataWhenEventResolverDoesNotKnowItsType()
    {
        var snapshot = new Excalibur.Domain.Model.Snapshot
        {
            SnapshotId = "snapshot", AggregateId = "aggregate", AggregateType = "Order", Version = 1,
            Data = new byte[] { 1 }, CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object> { ["custom"] = new { Label = "retained" } },
        };
        var request = SaveSnapshotRequest.CreateTyped(snapshot, Excalibur.Dispatch.TenantScope.Untenanted,
            CancellationToken.None, metadataResolver: System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine());
        using var document = System.Text.Json.JsonDocument.Parse(request.Parameters.Get<byte[]>("Metadata"));
        document.RootElement.GetProperty("custom").GetProperty("Label").GetString().ShouldBe("retained");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(4000)]
    [InlineData(4001)]
    public void PreserveStringBindingAndCommandOptions(int length)
    {
        using var connection = new SqlConnection();
        var parameters = new SqlServerParameterSet();
        var value = new string('x', length);
        parameters.Add("@Value", value);
        using var command = parameters.CreateCommand(connection,
            new CommandDefinition("SELECT @Value", parameters, commandTimeout: 17));
        command.CommandText.ShouldBe("SELECT @Value");
        command.CommandTimeout.ShouldBe(17);
        command.CommandType.ShouldBe(CommandType.Text);
        connection.State.ShouldBe(ConnectionState.Closed);
        var parameter = (SqlParameter)command.Parameters[0];
        parameter.Value.ShouldBe(value);
        parameter.SqlDbType.ShouldBe(SqlDbType.NVarChar);
        parameter.Size.ShouldBe(length <= 4000 ? 4000 : length);
    }

    [Fact]
    public void PreserveDriverDateTimeInferenceAndExplicitOverrides()
    {
        using var connection = new SqlConnection();
        var parameters = new SqlServerParameterSet();
        var instant = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        parameters.Add("inferred", instant);
        parameters.Add("explicit", instant, DbType.DateTime2);
        parameters.Add("empty", null, DbType.Binary);
        using var command = parameters.CreateCommand(connection, new CommandDefinition("SELECT 1", parameters));
        ((SqlParameter)command.Parameters[0]).SqlDbType.ShouldBe(SqlDbType.DateTime);
        ((SqlParameter)command.Parameters[1]).SqlDbType.ShouldBe(SqlDbType.DateTime2);
        command.Parameters[2].DbType.ShouldBe(DbType.Binary);
        command.Parameters[2].Value.ShouldBe(DBNull.Value);
    }
}
