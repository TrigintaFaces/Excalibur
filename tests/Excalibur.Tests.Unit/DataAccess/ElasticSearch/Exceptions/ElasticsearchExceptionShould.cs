using Excalibur.DataAccess.ElasticSearch.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch.Exceptions;

public class ElasticsearchExceptionShould
{
	[Fact]
	public void ConstructElasticsearchDeleteException()
	{
		var ex = new ElasticsearchDeleteException("doc-id", typeof(string), "API call details");
		ex.DocumentId.ShouldBe("doc-id");
		ex.DocumentType.ShouldBe(typeof(string));
		ex.ApiCallDetails.ShouldBe("API call details");
	}

	[Fact]
	public void ConstructElasticsearchGetByIdException()
	{
		var ex = new ElasticsearchGetByIdException("doc-id", typeof(int), "details");
		ex.DocumentId.ShouldBe("doc-id");
		ex.DocumentType.ShouldBe(typeof(int));
		ex.ApiCallDetails.ShouldBe("details");
	}

	[Fact]
	public void ConstructElasticsearchIndexingException()
	{
		var ex = new ElasticsearchIndexingException("index-1", typeof(DateTime), "some-api-details");
		ex.IndexName.ShouldBe("index-1");
		ex.DocumentType.ShouldBe(typeof(DateTime));
		ex.ApiCallDetails.ShouldBe("some-api-details");
	}

	[Fact]
	public void ConstructElasticsearchSearchException()
	{
		var ex = new ElasticsearchSearchException("index-name", typeof(Guid), "search-api");
		ex.IndexName.ShouldBe("index-name");
		ex.DocumentType.ShouldBe(typeof(Guid));
		ex.ApiCallDetails.ShouldBe("search-api");
	}

	[Fact]
	public void ConstructElasticsearchUpdateException()
	{
		var ex = new ElasticsearchUpdateException("doc-id", typeof(decimal), "update-details");
		ex.DocumentId.ShouldBe("doc-id");
		ex.DocumentType.ShouldBe(typeof(decimal));
		ex.ApiCallDetails.ShouldBe("update-details");
	}
}
