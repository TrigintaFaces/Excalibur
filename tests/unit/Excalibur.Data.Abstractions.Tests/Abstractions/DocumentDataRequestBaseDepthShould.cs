// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="DocumentDataRequestBase{TConnection, TResult}"/>.
/// Covers InitializeOperation, default property values, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DocumentDataRequestBaseDepthShould
{
	[Fact]
	public void HaveDefaultEmptyPropertyValues()
	{
		// Arrange & Act
		var request = new TestDocumentDataRequest();

		// Assert
		request.CollectionName.ShouldBe(string.Empty);
		request.OperationType.ShouldBe(string.Empty);
		request.Parameters.ShouldNotBeNull();
		request.Parameters.Count.ShouldBe(0);
		request.Options.ShouldBeNull();
	}

	[Fact]
	public void InitializeOperationWithAllParameters()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object> { ["key"] = "value", ["count"] = 42 };
		var options = new Dictionary<string, object> { ["option1"] = true };

		// Act
		request.CallInitializeOperation("users", "insert", parameters, options);

		// Assert
		request.CollectionName.ShouldBe("users");
		request.OperationType.ShouldBe("insert");
		request.Parameters.Count.ShouldBe(2);
		request.Parameters["key"].ShouldBe("value");
		request.Parameters["count"].ShouldBe(42);
		request.Options.ShouldNotBeNull();
		request.Options!["option1"].ShouldBe(true);
	}

	[Fact]
	public void InitializeOperationWithoutOptions()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object> { ["id"] = "abc" };

		// Act
		request.CallInitializeOperation("orders", "query", parameters);

		// Assert
		request.CollectionName.ShouldBe("orders");
		request.OperationType.ShouldBe("query");
		request.Parameters.Count.ShouldBe(1);
		request.Options.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenCollectionNameIsNull()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			request.CallInitializeOperation(null!, "insert", parameters));
	}

	[Fact]
	public void ThrowWhenCollectionNameIsEmpty()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			request.CallInitializeOperation("", "insert", parameters));
	}

	[Fact]
	public void ThrowWhenCollectionNameIsWhitespace()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			request.CallInitializeOperation("   ", "insert", parameters));
	}

	[Fact]
	public void ThrowWhenOperationTypeIsNull()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			request.CallInitializeOperation("users", null!, parameters));
	}

	[Fact]
	public void ThrowWhenOperationTypeIsEmpty()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			request.CallInitializeOperation("users", "", parameters));
	}

	[Fact]
	public void ThrowWhenParametersIsNull()
	{
		// Arrange
		var request = new TestDocumentDataRequest();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			request.CallInitializeOperation("users", "insert", null!));
	}

	[Fact]
	public void CreateDefensiveCopyOfParameters()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		request.CallInitializeOperation("users", "insert", parameters);
		parameters["key"] = "modified";

		// Assert - original parameters should be unchanged
		request.Parameters["key"].ShouldBe("value");
	}

	[Fact]
	public void CreateDefensiveCopyOfOptions()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object> { ["key"] = "value" };
		var options = new Dictionary<string, object> { ["opt"] = "original" };

		// Act
		request.CallInitializeOperation("users", "insert", parameters, options);
		options["opt"] = "modified";

		// Assert - original options should be unchanged
		request.Options!["opt"].ShouldBe("original");
	}

	[Fact]
	public void AllowReInitializationOverwritingPreviousValues()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters1 = new Dictionary<string, object> { ["key"] = "value1" };
		var parameters2 = new Dictionary<string, object> { ["key"] = "value2" };

		// Act
		request.CallInitializeOperation("collection1", "insert", parameters1);
		request.CallInitializeOperation("collection2", "update", parameters2);

		// Assert
		request.CollectionName.ShouldBe("collection2");
		request.OperationType.ShouldBe("update");
		request.Parameters["key"].ShouldBe("value2");
	}

	[Fact]
	public void UseOrdinalStringComparerForParameters()
	{
		// Arrange
		var request = new TestDocumentDataRequest();
		var parameters = new Dictionary<string, object>
		{
			["CaseSensitive"] = "upper",
			["casesensitive"] = "lower",
		};

		// Act
		request.CallInitializeOperation("col", "op", parameters);

		// Assert - ordinal comparer means both keys should be preserved
		request.Parameters.Count.ShouldBe(2);
	}

	/// <summary>
	/// Test implementation of DocumentDataRequestBase for testing purposes.
	/// </summary>
	private sealed class TestDocumentDataRequest : DocumentDataRequestBase<object, string>
	{
		public void CallInitializeOperation(
			string collectionName,
			string operationType,
			IDictionary<string, object> parameters,
			IDictionary<string, object>? options = null)
		{
			InitializeOperation(collectionName, operationType, parameters, options);
		}
	}
}
