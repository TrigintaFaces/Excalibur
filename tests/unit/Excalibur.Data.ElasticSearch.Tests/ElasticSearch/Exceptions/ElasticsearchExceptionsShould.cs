// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Exceptions;

namespace Excalibur.Data.Tests.ElasticSearch.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchExceptionsShould
{
	// ElasticsearchDeleteException

	[Fact]
	public void CreateDeleteExceptionWithAllDetails()
	{
		// Arrange & Act
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchDeleteException("doc-123", typeof(string), "API call details", inner);

		// Assert
		ex.DocumentId.ShouldBe("doc-123");
		ex.DocumentType.ShouldBe(typeof(string));
		ex.ApiCallDetails.ShouldBe("API call details");
		ex.InnerException.ShouldBe(inner);
		ex.Message.ShouldContain("doc-123");
		ex.Message.ShouldContain("String");
	}

	[Fact]
	public void ThrowDeleteExceptionWhenDocumentIdIsNull()
	{
		Should.Throw<ArgumentException>(
			() => new ElasticsearchDeleteException(null!, typeof(string), "details"));
	}

	[Fact]
	public void ThrowDeleteExceptionWhenApiCallDetailsIsNull()
	{
		Should.Throw<ArgumentException>(
			() => new ElasticsearchDeleteException("doc-123", typeof(string), null!));
	}

	[Fact]
	public void CreateDeleteExceptionWithDefaultConstructor()
	{
		var ex = new ElasticsearchDeleteException();
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateDeleteExceptionWithMessage()
	{
		var ex = new ElasticsearchDeleteException("test message");
		ex.Message.ShouldBe("test message");
	}

	[Fact]
	public void CreateDeleteExceptionWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchDeleteException("test message", inner);
		ex.Message.ShouldBe("test message");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateDeleteExceptionWithStatusCodeMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchDeleteException(404, "not found", inner);
		ex.StatusCode.ShouldBe(404);
		ex.InnerException.ShouldBe(inner);
	}

	// ElasticsearchGetByIdException

	[Fact]
	public void CreateGetByIdExceptionWithAllDetails()
	{
		var ex = new ElasticsearchGetByIdException("doc-456", typeof(int), "API error");

		ex.DocumentId.ShouldBe("doc-456");
		ex.DocumentType.ShouldBe(typeof(int));
		ex.ApiCallDetails.ShouldBe("API error");
		ex.Message.ShouldContain("doc-456");
	}

	[Fact]
	public void CreateGetByIdExceptionWithDefaultConstructor()
	{
		var ex = new ElasticsearchGetByIdException();
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateGetByIdExceptionWithMessage()
	{
		var ex = new ElasticsearchGetByIdException("test msg");
		ex.Message.ShouldBe("test msg");
	}

	[Fact]
	public void CreateGetByIdExceptionWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchGetByIdException("test msg", inner);
		ex.InnerException.ShouldBe(inner);
	}

	// ElasticsearchIndexingException

	[Fact]
	public void CreateIndexingExceptionWithAllDetails()
	{
		var ex = new ElasticsearchIndexingException("my-index", typeof(string), "API details");

		ex.IndexName.ShouldBe("my-index");
		ex.DocumentType.ShouldBe(typeof(string));
		ex.ApiCallDetails.ShouldBe("API details");
		ex.Message.ShouldContain("my-index");
	}

	[Fact]
	public void CreateIndexingExceptionWithDefaultConstructor()
	{
		var ex = new ElasticsearchIndexingException();
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateIndexingExceptionWithMessage()
	{
		var ex = new ElasticsearchIndexingException("test");
		ex.Message.ShouldBe("test");
	}

	[Fact]
	public void CreateIndexingExceptionWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchIndexingException("test", inner);
		ex.InnerException.ShouldBe(inner);
	}

	// ElasticsearchSearchException

	[Fact]
	public void CreateSearchExceptionWithAllDetails()
	{
		var ex = new ElasticsearchSearchException("search-index", typeof(double), "API details");

		ex.IndexName.ShouldBe("search-index");
		ex.DocumentType.ShouldBe(typeof(double));
		ex.ApiCallDetails.ShouldBe("API details");
	}

	[Fact]
	public void CreateSearchExceptionWithDefaultConstructor()
	{
		var ex = new ElasticsearchSearchException();
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSearchExceptionWithMessage()
	{
		var ex = new ElasticsearchSearchException("test");
		ex.Message.ShouldBe("test");
	}

	[Fact]
	public void CreateSearchExceptionWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchSearchException("test", inner);
		ex.InnerException.ShouldBe(inner);
	}

	// ElasticsearchUpdateException

	[Fact]
	public void CreateUpdateExceptionWithAllDetails()
	{
		var ex = new ElasticsearchUpdateException("doc-789", typeof(int), "API details");

		ex.DocumentId.ShouldBe("doc-789");
		ex.DocumentType.ShouldBe(typeof(int));
		ex.ApiCallDetails.ShouldBe("API details");
	}

	[Fact]
	public void CreateUpdateExceptionWithDefaultConstructor()
	{
		var ex = new ElasticsearchUpdateException();
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUpdateExceptionWithMessage()
	{
		var ex = new ElasticsearchUpdateException("test");
		ex.Message.ShouldBe("test");
	}

	[Fact]
	public void CreateUpdateExceptionWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ElasticsearchUpdateException("test", inner);
		ex.InnerException.ShouldBe(inner);
	}
}
