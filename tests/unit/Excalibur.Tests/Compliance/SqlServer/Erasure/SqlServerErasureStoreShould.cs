// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Compliance.SqlServer.Erasure;

/// <summary>
/// Unit tests for <see cref="SqlServerErasureStore"/>.
/// Tests constructor validation and GetService behavior without requiring a real database.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class SqlServerErasureStoreShould : UnitTestBase
{
	private static readonly SqlServerErasureStoreOptions ValidOptions = new()
	{
		ConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true",
		AutoCreateSchema = false // prevent real DB calls
	};

	private SqlServerErasureStore CreateStore(SqlServerErasureStoreOptions? options = null)
	{
		var opts = options ?? ValidOptions;
		return new SqlServerErasureStore(
			Microsoft.Extensions.Options.Options.Create(opts),
			NullLogger<SqlServerErasureStore>.Instance);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerErasureStore(null!, NullLogger<SqlServerErasureStore>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerErasureStore(
				Microsoft.Extensions.Options.Options.Create(ValidOptions),
				null!));
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenOptionsAreInvalid()
	{
		var invalidOptions = new SqlServerErasureStoreOptions
		{
			ConnectionString = ""
		};

		Should.Throw<InvalidOperationException>(() =>
			new SqlServerErasureStore(
				Microsoft.Extensions.Options.Options.Create(invalidOptions),
				NullLogger<SqlServerErasureStore>.Instance));
	}

	[Fact]
	public void Constructor_Succeeds_WhenOptionsAreValid()
	{
		// Act & Assert - should not throw
		var store = CreateStore();
		store.ShouldNotBeNull();
	}

	[Fact]
	public void GetService_ReturnsSelf_ForIErasureCertificateStore()
	{
		// Arrange
		var store = CreateStore();

		// Act
		var result = store.GetService(typeof(IErasureCertificateStore));

		// Assert
		result.ShouldBe(store);
	}

	[Fact]
	public void GetService_ReturnsSelf_ForIErasureQueryStore()
	{
		// Arrange
		var store = CreateStore();

		// Act
		var result = store.GetService(typeof(IErasureQueryStore));

		// Assert
		result.ShouldBe(store);
	}

	[Fact]
	public void GetService_ReturnsNull_ForUnknownType()
	{
		// Arrange
		var store = CreateStore();

		// Act
		var result = store.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetService_ThrowsArgumentNullException_WhenServiceTypeIsNull()
	{
		// Arrange
		var store = CreateStore();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}

	[Fact]
	public void ImplementsIErasureStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IErasureStore>();
	}

	[Fact]
	public void ImplementsIErasureCertificateStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IErasureCertificateStore>();
	}

	[Fact]
	public void ImplementsIErasureQueryStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IErasureQueryStore>();
	}
}
