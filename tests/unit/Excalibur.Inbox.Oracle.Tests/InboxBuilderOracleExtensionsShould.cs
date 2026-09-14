// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Regression lock for the builder composition path every sibling inbox provider supports.
/// </summary>
/// <remarks>
/// <para>
/// Oracle previously exposed only <c>AddOracleInboxStore</c> on <see cref="IServiceCollection"/>. A
/// consumer composing through <c>AddExcaliburInbox(inbox =&gt; inbox.Use…())</c> — the documented shape
/// for Postgres, SQL Server, Cosmos, DynamoDB, Firestore, Mongo, Redis and ElasticSearch — had no Oracle
/// arm, so the provider bundled in the Oracle metapackage could not be reached that way at all.
/// </para>
/// <para>
/// The store resolution arm is what makes this non-vacuous: an extension that registered options and a
/// connection factory but no <see cref="IInboxStore"/> would satisfy a registration-count assertion while
/// leaving the provider advertised-but-unwired, which is the defect class this repository keeps finding.
/// Resolution is checked under both the provider key and the default key, with no live database, because
/// construction — not connection — is what the composition path owes.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Persistence")]
[Trait("Database", "Oracle")]
public sealed class InboxBuilderOracleExtensionsShould
{
	private const string TestConnectionString = "User Id=u;Password=p;Data Source=//localhost:1521/FREEPDB1";

	private sealed class TestInboxBuilder : IInboxBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseOracle(oracle => oracle.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		var builder = new TestInboxBuilder();

		_ = Should.Throw<ArgumentNullException>(() => builder.UseOracle((Action<IOracleInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		var builder = new TestInboxBuilder();

		var result = builder.UseOracle(oracle => oracle.ConnectionString(TestConnectionString));

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void CarryBuilderConfiguration_IntoTheResolvedOptions()
	{
		var builder = new TestInboxBuilder();

		_ = builder.UseOracle(oracle => oracle
			.ConnectionString(TestConnectionString)
			.SchemaName("MESSAGING")
			.TableName("INBOX_MSGS")
			.CommandTimeoutSeconds(45)
			.MaxRetryCount(7));

		using var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<OracleInboxOptions>>().Value;

		options.ConnectionString.ShouldBe(TestConnectionString);
		options.SchemaName.ShouldBe("MESSAGING");
		options.TableName.ShouldBe("INBOX_MSGS");
		options.CommandTimeoutSeconds.ShouldBe(45);
		options.MaxRetryCount.ShouldBe(7);
	}

	[Fact]
	public void ResolveTheOracleInboxStore_UnderBothTheProviderKeyAndTheDefaultKey()
	{
		var builder = new TestInboxBuilder();

		_ = builder.UseOracle(oracle => oracle.ConnectionString(TestConnectionString));

		using var provider = builder.Services.BuildServiceProvider();

		provider.GetRequiredKeyedService<IInboxStore>("oracle").ShouldBeOfType<OracleInboxStore>();
		provider.GetRequiredKeyedService<IInboxStore>("default").ShouldBeOfType<OracleInboxStore>();
	}

	[Fact]
	public void BuildTheStoreFromTheBuilderConnectionFactory_WhenOneIsSupplied()
	{
		var builder = new TestInboxBuilder();
		var factoryWasUsed = false;

		_ = builder.UseOracle(oracle => oracle.ConnectionFactory(_ =>
		{
			factoryWasUsed = true;
			return () => new global::Oracle.ManagedDataAccess.Client.OracleConnection(TestConnectionString);
		}));

		using var provider = builder.Services.BuildServiceProvider();

		_ = provider.GetRequiredKeyedService<IInboxStore>("oracle").ShouldBeOfType<OracleInboxStore>();
		factoryWasUsed.ShouldBeTrue(
			"a connection factory configured on the builder must reach the store; ignoring it would silently "
			+ "fall back to the options connection string and defeat wallet or managed-credential wiring");
	}

	[Fact]
	public void WireStartupValidation_SoAMisconfiguredBuilderPathFailsFast()
	{
		var builder = new TestInboxBuilder();

		_ = builder.UseOracle(oracle => oracle.ConnectionString(TestConnectionString).TableName("VALID_NAME"));

		using var provider = builder.Services.BuildServiceProvider();

		provider.GetService<IStartupValidator>().ShouldNotBeNull();
	}

	[Fact]
	public void RejectAnUnsafeIdentifier_SuppliedThroughTheBuilder()
	{
		var builder = new TestInboxBuilder();

		_ = builder.UseOracle(oracle => oracle.ConnectionString(TestConnectionString).TableName("INBOX; DROP TABLE X--"));

		using var provider = builder.Services.BuildServiceProvider();
		var validator = provider.GetRequiredService<IStartupValidator>();

		// LIVENESS: the identifier allowlist must still refuse a hostile table name on the builder path.
		// Without this arm a validator that never runs, or one wired to accept everything, would pass the
		// arms above unnoticed.
		_ = Should.Throw<OptionsValidationException>(validator.Validate);
	}
}
