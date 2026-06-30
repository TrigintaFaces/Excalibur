// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance;
using Excalibur.Security;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Database", "SqlServer")]
public sealed class SqlSecurityEventStoreBridgeIntegrationShould : IntegrationTestBase
{
	private const string SourceIpSentinel = "203.0.113.77";
	private const string UserAgentSentinel = "SENTINEL-UA/MozillaForensicAgent-9z";
	private const string AdditionalDataSecretSentinel = "SENTINEL-FORENSIC-PAYLOAD-do-not-persist-in-metadata";

	private readonly SqlServerFixture _fixture;

	public SqlSecurityEventStoreBridgeIntegrationShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task RoundTripSecurityEventThroughRealSql_AndNeverLeakSensitiveValuesIntoTheMetadataBag()
	{
		_fixture.ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"a real SQL Server (TestContainers) is required -- this engage-test is never skipped");

		await InitializeAuditTableAsync();

		var auditOptions = new SqlServerAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = "AuditEvents",
			CommandTimeoutSeconds = 30,
			Retention = { CleanupBatchSize = 100 },
		};

		var services = new ServiceCollection();
		_ = services.AddSingleton<IAuditStore>(_ => new SqlServerAuditStore(
			Microsoft.Extensions.Options.Options.Create(auditOptions),
			AuditIntegrityTestStrategy.Create(),
			EnabledTestLogger.Create<SqlServerAuditStore>()));
		_ = services.AddSqlSecurityEventStore();

		await using var provider = services.BuildServiceProvider();
		var securityStore = provider.GetRequiredService<ISecurityEventStore>();

		var eventId = Guid.NewGuid();
		var correlationId = Guid.NewGuid();
		var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1);

		var securityEvent = new SecurityEvent
		{
			Id = eventId,
			Timestamp = timestamp,
			EventType = SecurityEventType.AuthenticationFailure,
			Description = "failed login from unrecognized device",
			Severity = SecuritySeverity.High,
			CorrelationId = correlationId,
			UserId = "user-7788",
			SourceIp = SourceIpSentinel,
			UserAgent = UserAgentSentinel,
			MessageType = "LoginCommand",
			AdditionalData = new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["rawForensicBlob"] = AdditionalDataSecretSentinel,
			},
		};

		await securityStore.StoreEventsAsync([securityEvent], TestCancellationToken);

		var loaded = (await securityStore.QueryEventsAsync(
			new SecurityEventQuery
			{
				StartTime = timestamp.AddMinutes(-5),
				EndTime = timestamp.AddMinutes(5),
				EventType = SecurityEventType.AuthenticationFailure,
				MaxResults = 50,
			},
			TestCancellationToken)).ToList();

		var restored = loaded.ShouldHaveSingleItem();
		restored.Id.ShouldBe(eventId);
		restored.EventType.ShouldBe(SecurityEventType.AuthenticationFailure);
		restored.Description.ShouldBe("failed login from unrecognized device");
		restored.Severity.ShouldBe(SecuritySeverity.High);
		restored.CorrelationId.ShouldBe(correlationId);
		restored.UserId.ShouldBe("user-7788");
		restored.SourceIp.ShouldBe(SourceIpSentinel, "SourceIp maps to the first-class [IpAddress] column and round-trips");
		restored.UserAgent.ShouldBe(UserAgentSentinel, "UserAgent maps to the first-class [UserAgent] column and round-trips");
		restored.MessageType.ShouldBe("LoginCommand");
		restored.AdditionalData.ShouldBeEmpty("the forensic AdditionalData payload has no compliant home in AuditEvent and is not restored");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		var row = await connection.QuerySingleAsync<(string? IpAddress, string? UserAgent, string? Metadata)>(
			"SELECT IpAddress, UserAgent, Metadata FROM [audit].[AuditEvents] WHERE EventId = @EventId",
			new { EventId = eventId.ToString() });

		row.IpAddress.ShouldBe(SourceIpSentinel, "SourceIp is persisted in the dedicated [IpAddress] column");
		row.UserAgent.ShouldBe(UserAgentSentinel, "UserAgent is persisted in the dedicated [UserAgent] column");

		var metadata = row.Metadata ?? string.Empty;
		metadata.Contains("security.severity", StringComparison.Ordinal).ShouldBeTrue("severity is recorded as a reference key in Metadata");
		metadata.Contains("security.messageType", StringComparison.Ordinal).ShouldBeTrue("messageType is recorded as a reference key in Metadata");

		metadata.Contains(SourceIpSentinel, StringComparison.Ordinal).ShouldBeFalse("the SourceIp must not leak into the free-form Metadata bag");
		metadata.Contains(UserAgentSentinel, StringComparison.Ordinal).ShouldBeFalse("the UserAgent must not leak into the free-form Metadata bag");
		metadata.Contains(AdditionalDataSecretSentinel, StringComparison.Ordinal).ShouldBeFalse("arbitrary AdditionalData forensic payload must not leak into the Metadata bag");
	}

	private async Task InitializeAuditTableAsync()
	{
		const string createSchemaAndTableSql = """
			IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
			BEGIN
			    EXEC('CREATE SCHEMA [audit]');
			END;

			IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [audit].[AuditEvents] (
			        [SequenceNumber] BIGINT IDENTITY(1,1) NOT NULL,
			        [EventId] NVARCHAR(64) NOT NULL,
			        [EventType] INT NOT NULL,
			        [Action] NVARCHAR(100) NOT NULL,
			        [Outcome] INT NOT NULL,
			        [Timestamp] DATETIMEOFFSET(7) NOT NULL,
			        [ActorId] NVARCHAR(256) NOT NULL,
			        [ActorType] NVARCHAR(50) NULL,
			        [ResourceId] NVARCHAR(256) NULL,
			        [ResourceType] NVARCHAR(100) NULL,
			        [ResourceClassification] INT NULL,
			        [TenantId] NVARCHAR(64) NULL,
			        [ApplicationName] NVARCHAR(256) NULL,
			        [CorrelationId] NVARCHAR(64) NULL,
			        [SessionId] NVARCHAR(64) NULL,
			        [IpAddress] NVARCHAR(45) NULL,
			        [UserAgent] NVARCHAR(500) NULL,
			        [Reason] NVARCHAR(1000) NULL,
			        [Metadata] NVARCHAR(MAX) NULL,
			        [PreviousEventHash] NVARCHAR(512) NULL,
			        [EventHash] NVARCHAR(512) NOT NULL,
			        CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([SequenceNumber] ASC),
			        CONSTRAINT [UQ_AuditEvents_EventId] UNIQUE NONCLUSTERED ([EventId])
			    );
			END;

			DELETE FROM [audit].[AuditEvents];
			""";

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(createSchemaAndTableSql);
	}
}
