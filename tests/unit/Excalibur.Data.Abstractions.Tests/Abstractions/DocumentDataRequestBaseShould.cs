// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class DocumentDataRequestBaseShould
{
	[Fact]
	public void ImplementIDocumentDataRequest()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.ShouldBeAssignableTo<IDocumentDataRequest<IDbConnection, string>>();
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.CollectionName.ShouldBe("TestCollection");
	}

	[Fact]
	public void HaveDefaultOperationType()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.OperationType.ShouldBe("Find");
	}

	[Fact]
	public void HaveResolveAsyncFunction()
	{
		// Arrange
		var request = new TestDocumentDataRequest();

		// Act & Assert
		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteResolveAsync()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var connection = A.Fake<IDbConnection>();

		// Act
		var result = await request.ResolveAsync(connection);

		// Assert
		result.ShouldBe("TestResult");
	}

	[Fact]
	public void HaveDefaultEmptyParameters()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.Parameters.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNullOptionsWhenNotSet()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.Options.ShouldBeNull();
	}

	[Fact]
	public void InitializeOperationWithRequiredParameters()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequestWithInit("Users", "Insert",
			new Dictionary<string, object>(StringComparer.Ordinal) { ["name"] = "test" });

		// Assert
		request.CollectionName.ShouldBe("Users");
		request.OperationType.ShouldBe("Insert");
		request.Parameters["name"].ShouldBe("test");
		request.Options.ShouldBeNull();
	}

	[Fact]
	public void InitializeOperationWithOptions()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequestWithInit("Users", "Insert",
			new Dictionary<string, object>(StringComparer.Ordinal) { ["name"] = "test" },
			new Dictionary<string, object>(StringComparer.Ordinal) { ["writeConcern"] = "majority" });

		// Assert
		request.Options.ShouldNotBeNull();
		request.Options!["writeConcern"].ShouldBe("majority");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenCollectionNameIsInvalid(string? collectionName)
	{
		Should.Throw<ArgumentException>(() =>
			new TestDocumentDataRequestWithInit(collectionName!, "Insert",
				new Dictionary<string, object>(StringComparer.Ordinal)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenOperationTypeIsInvalid(string? operationType)
	{
		Should.Throw<ArgumentException>(() =>
			new TestDocumentDataRequestWithInit("Users", operationType!,
				new Dictionary<string, object>(StringComparer.Ordinal)));
	}

	[Fact]
	public void ThrowWhenParametersAreNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new TestDocumentDataRequestWithInit("Users", "Insert", null!));
	}

	private sealed class TestDocumentDataRequest : DocumentDataRequestBase<IDbConnection, string>
	{
		public TestDocumentDataRequest()
		{
			CollectionName = "TestCollection";
			OperationType = "Find";
			ResolveAsync = static _ => Task.FromResult("TestResult");
		}
	}

	private sealed class TestDocumentDataRequestWithInit : DocumentDataRequestBase<IDbConnection, string>
	{
		public TestDocumentDataRequestWithInit(
			string collectionName,
			string operationType,
			IDictionary<string, object> parameters,
			IDictionary<string, object>? options = null)
		{
			ResolveAsync = static _ => Task.FromResult("InitResult");
			InitializeOperation(collectionName, operationType, parameters, options);
		}
	}
}
