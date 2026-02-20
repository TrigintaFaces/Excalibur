// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DeadLetterDocumentShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var doc = new DeadLetterDocument<string>();

		doc.OriginalDocument.ShouldBeNull();
		doc.ErrorMessage.ShouldBe(string.Empty);
		doc.ErrorType.ShouldBe(string.Empty);
		doc.Timestamp.ShouldBe(default);
		doc.RetryCount.ShouldBe(0);
	}

	[Fact]
	public void SetAllProperties()
	{
		var timestamp = DateTimeOffset.UtcNow;
		var doc = new DeadLetterDocument<string>
		{
			OriginalDocument = "test-doc",
			ErrorMessage = "Something failed",
			ErrorType = "InvalidOperationException",
			Timestamp = timestamp,
			RetryCount = 3,
		};

		doc.OriginalDocument.ShouldBe("test-doc");
		doc.ErrorMessage.ShouldBe("Something failed");
		doc.ErrorType.ShouldBe("InvalidOperationException");
		doc.Timestamp.ShouldBe(timestamp);
		doc.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void WorkWithComplexDocumentType()
	{
		var doc = new DeadLetterDocument<TestDocument>
		{
			OriginalDocument = new TestDocument { Id = "123", Name = "Test" },
			ErrorMessage = "Index failure",
			ErrorType = "ElasticsearchIndexingException",
		};

		doc.OriginalDocument.ShouldNotBeNull();
		doc.OriginalDocument.Id.ShouldBe("123");
		doc.OriginalDocument.Name.ShouldBe("Test");
	}

	[Fact]
	public void AllowNullOriginalDocument()
	{
		var doc = new DeadLetterDocument<string> { OriginalDocument = null };

		doc.OriginalDocument.ShouldBeNull();
	}

	private sealed class TestDocument
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
