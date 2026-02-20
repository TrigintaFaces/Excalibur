using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Postgres.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresAuditStoreShould
{
	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PostgresAuditStore(
				null!,
				NullLogger<PostgresAuditStore>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit"
		});

		Should.Throw<ArgumentNullException>(() =>
			new PostgresAuditStore(options, null!));
	}

	[Fact]
	public void Throw_for_empty_connection_string()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = string.Empty
		});

		Should.Throw<ArgumentException>(() =>
			new PostgresAuditStore(
				options,
				NullLogger<PostgresAuditStore>.Instance));
	}

	[Fact]
	public void Throw_for_null_connection_string()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = null!
		});

		Should.Throw<ArgumentException>(() =>
			new PostgresAuditStore(
				options,
				NullLogger<PostgresAuditStore>.Instance));
	}

	[Theory]
	[InlineData("invalid-schema")]
	[InlineData("schema.name")]
	[InlineData("schema name")]
	[InlineData("schema;drop")]
	[InlineData("schema'name")]
	public void Throw_for_invalid_schema_name(string schemaName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			SchemaName = schemaName
		});

		Should.Throw<ArgumentException>(() =>
			new PostgresAuditStore(
				options,
				NullLogger<PostgresAuditStore>.Instance));
	}

	[Theory]
	[InlineData("invalid-table")]
	[InlineData("table.name")]
	[InlineData("table name")]
	[InlineData("table;drop")]
	[InlineData("table'name")]
	public void Throw_for_invalid_table_name(string tableName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			TableName = tableName
		});

		Should.Throw<ArgumentException>(() =>
			new PostgresAuditStore(
				options,
				NullLogger<PostgresAuditStore>.Instance));
	}

	[Theory]
	[InlineData("audit")]
	[InlineData("AUDIT")]
	[InlineData("audit_events")]
	[InlineData("my_schema_123")]
	[InlineData("A")]
	public void Accept_valid_schema_names(string schemaName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			SchemaName = schemaName
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		store.ShouldNotBeNull();
		store.Dispose();
	}

	[Theory]
	[InlineData("audit_events")]
	[InlineData("AUDIT_EVENTS")]
	[InlineData("events123")]
	[InlineData("T")]
	public void Accept_valid_table_names(string tableName)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			TableName = tableName
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		store.ShouldNotBeNull();
		store.Dispose();
	}

	[Fact]
	public void Be_disposable()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit"
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		// Should not throw
		store.Dispose();
	}

	[Fact]
	public void Handle_double_dispose_safely()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit"
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		// Double dispose should not throw
		store.Dispose();
		store.Dispose();
	}

	[Fact]
	public void Implement_IAuditStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit"
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		store.ShouldBeAssignableTo<Excalibur.Dispatch.Compliance.IAuditStore>();
		store.Dispose();
	}

	[Fact]
	public void Implement_IDisposable()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit"
		});

		var store = new PostgresAuditStore(
			options,
			NullLogger<PostgresAuditStore>.Instance);

		store.ShouldBeAssignableTo<IDisposable>();
		store.Dispose();
	}
}
