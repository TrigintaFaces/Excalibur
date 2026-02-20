// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.SqlServer.Erasure;

namespace Excalibur.Tests.Compliance.SqlServer.Erasure;

/// <summary>
/// Unit tests for <see cref="SqlServerErasureStoreOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class SqlServerErasureStoreOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqlServerErasureStoreOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.SchemaName.ShouldBe("compliance");
		options.RequestsTableName.ShouldBe("ErasureRequests");
		options.CertificatesTableName.ShouldBe("ErasureCertificates");
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.AutoCreateSchema.ShouldBeTrue();
	}

	[Fact]
	public void ComputeFullRequestsTableName()
	{
		var options = new SqlServerErasureStoreOptions
		{
			SchemaName = "gdpr",
			RequestsTableName = "Requests"
		};

		options.FullRequestsTableName.ShouldBe("[gdpr].[Requests]");
	}

	[Fact]
	public void ComputeFullCertificatesTableName()
	{
		var options = new SqlServerErasureStoreOptions
		{
			SchemaName = "gdpr",
			CertificatesTableName = "Certs"
		};

		options.FullCertificatesTableName.ShouldBe("[gdpr].[Certs]");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenConnectionStringIsEmpty()
	{
		var options = new SqlServerErasureStoreOptions { ConnectionString = "" };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenConnectionStringIsWhitespace()
	{
		var options = new SqlServerErasureStoreOptions { ConnectionString = "   " };

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenSchemaNameIsEmpty()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			SchemaName = ""
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenRequestsTableNameIsEmpty()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			RequestsTableName = ""
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCertificatesTableNameIsEmpty()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			CertificatesTableName = ""
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCommandTimeoutIsZero()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			CommandTimeoutSeconds = 0
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCommandTimeoutIsNegative()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			CommandTimeoutSeconds = -1
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenSchemaNameHasInvalidChars()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			SchemaName = "drop; --"
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenRequestsTableNameHasInvalidChars()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			RequestsTableName = "Robert'; DROP TABLE Students; --"
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCertificatesTableNameHasInvalidChars()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			CertificatesTableName = "bad name!"
		};

		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_Succeeds_WhenAllOptionsAreValid()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true",
			SchemaName = "compliance",
			RequestsTableName = "ErasureRequests",
			CertificatesTableName = "ErasureCertificates",
			CommandTimeoutSeconds = 60
		};

		// Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WithUnderscoresInNames()
	{
		var options = new SqlServerErasureStoreOptions
		{
			ConnectionString = "Server=test",
			SchemaName = "my_schema",
			RequestsTableName = "erasure_requests",
			CertificatesTableName = "erasure_certificates"
		};

		// Should not throw
		options.Validate();
	}
}
