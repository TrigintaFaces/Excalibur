// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using SqlServerNs = Excalibur.EventSourcing.SqlServer;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Tests for <see cref="Excalibur.EventSourcing.SqlServerNs.SqlServerMaterializedViewStore"/>.
/// Covers constructor validation, table name customization, and SQL identifier safety.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerMaterializedViewStoreShould
{
	private static readonly Func<SqlConnection> ValidConnectionFactory = () => new SqlConnection("Server=localhost");
	private static readonly ILogger<SqlServerNs.SqlServerMaterializedViewStore> Logger =
		NullLogger<SqlServerNs.SqlServerMaterializedViewStore>.Instance;

	// ═══════════════════════════════════════════════════
	// Constructor — connection factory overload
	// ═══════════════════════════════════════════════════

	[Fact]
	public void CreateWithDefaultTableNames()
	{
		// Act — should not throw with valid args and default table names
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

		// Assert — instance created (no public property for table names, so we verify no exception)
		store.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConnectionFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore((Func<SqlConnection>)null!, Logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, null!));
	}

	// ═══════════════════════════════════════════════════
	// Constructor — connection string overload
	// ═══════════════════════════════════════════════════

	[Fact]
	public void CreateWithConnectionString()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(
			"Server=localhost;Database=TestDb",
			Logger);

		store.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsNull()
	{
		Should.Throw<ArgumentException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore((string)null!, Logger));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsWhitespace()
	{
		Should.Throw<ArgumentException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore("   ", Logger));
	}

	// ═══════════════════════════════════════════════════
	// Constructor — custom table names
	// ═══════════════════════════════════════════════════

	[Fact]
	public void AcceptCustomTableNames()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(
			ValidConnectionFactory,
			Logger,
			viewTableName: "CustomViews",
			positionTableName: "CustomPositions");

		store.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnInvalidViewTableName_SqlInjectionAttempt()
	{
		// SQL identifier validation should reject table names with SQL injection characters
		Should.Throw<ArgumentException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore(
				ValidConnectionFactory,
				Logger,
				viewTableName: "DROP TABLE Users; --"));
	}

	[Fact]
	public void ThrowOnInvalidPositionTableName_SqlInjectionAttempt()
	{
		Should.Throw<ArgumentException>(() =>
			new SqlServerNs.SqlServerMaterializedViewStore(
				ValidConnectionFactory,
				Logger,
				positionTableName: "'; DROP TABLE Events; --"));
	}

	// ═══════════════════════════════════════════════════
	// Method argument validation (without real SQL connection)
	// ═══════════════════════════════════════════════════

	[Fact]
	public async Task GetAsync_ThrowsWhenViewNameIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(null!, "id-1", CancellationToken.None));
#pragma warning restore IL3050
#pragma warning restore IL2026
	}

	[Fact]
	public async Task GetAsync_ThrowsWhenViewIdIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

#pragma warning disable IL2026
#pragma warning disable IL3050
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("ViewName", null!, CancellationToken.None));
#pragma warning restore IL3050
#pragma warning restore IL2026
	}

	[Fact]
	public async Task SaveAsync_ThrowsWhenViewNameIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

#pragma warning disable IL2026
#pragma warning disable IL3050
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync<TestView>(null!, "id-1", new TestView(), CancellationToken.None));
#pragma warning restore IL3050
#pragma warning restore IL2026
	}

	[Fact]
	public async Task SaveAsync_ThrowsWhenViewIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

#pragma warning disable IL2026
#pragma warning disable IL3050
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.SaveAsync<TestView>("ViewName", "id-1", null!, CancellationToken.None));
#pragma warning restore IL3050
#pragma warning restore IL2026
	}

	[Fact]
	public async Task DeleteAsync_ThrowsWhenViewNameIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(null!, "id-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowsWhenViewNameIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowsWhenViewNameIsNull()
	{
		var store = new SqlServerNs.SqlServerMaterializedViewStore(ValidConnectionFactory, Logger);

		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(null!, 100L, CancellationToken.None));
	}

	// ═══════════════════════════════════════════════════
	// Test helper types
	// ═══════════════════════════════════════════════════

	private sealed class TestView
	{
		public string Name { get; set; } = string.Empty;
	}
}
