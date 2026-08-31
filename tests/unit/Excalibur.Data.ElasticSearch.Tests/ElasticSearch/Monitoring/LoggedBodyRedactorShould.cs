// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for <see cref="LoggedBodyRedactor"/>.
/// </summary>
/// <remarks>
/// These cover both halves of the contract. The safety half is that a value the allow list does not name is never
/// written out; the liveness half is that a value it does name still is, and that the surrounding structure survives -
/// a redactor that emitted nothing at all would satisfy every safety assertion here and be useless.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class LoggedBodyRedactorShould
{
	private static readonly HashSet<string> None = new(StringComparer.OrdinalIgnoreCase);

	private static HashSet<string> Allow(params string[] names) => new(names, StringComparer.OrdinalIgnoreCase);

	#region Safety - values that were never named must not reach the log

	[Fact]
	public void RedactAValueWhoseNameIsNotOnTheAllowList()
	{
		// Arrange - a field name the previous deny list never covered
		const string body = """{"ssn":"123-45-6789"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotContain("123-45-6789");
		redacted.ShouldBe("""{"ssn":"[Redacted]"}""");
	}

	[Theory]
	[InlineData("clientSecret")]
	[InlineData("privateKey")]
	[InlineData("accessKey")]
	[InlineData("sessionId")]
	[InlineData("creditCard")]
	[InlineData("email")]
	[InlineData("dateOfBirth")]
	public void RedactAnyFieldNobodyAnticipated(string fieldName)
	{
		// Arrange
		var body = $$"""{"{{fieldName}}":"the-secret-value"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotContain("the-secret-value");
	}

	[Fact]
	public void RedactNumericAndBooleanValuesToo()
	{
		// Arrange - a national identifier or card number is no less sensitive for being a number
		const string body = """{"accountNumber":4111111111111111,"isVip":true}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotContain("4111111111111111");
		redacted.ShouldNotContain("true");
	}

	[Fact]
	public void RedactValuesNestedUnderAnUnlistedProperty()
	{
		// Arrange
		const string body = """{"query":{"match":{"email":"person@example.com"}}}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotContain("person@example.com");
	}

	[Fact]
	public void RedactValuesInsideArrays()
	{
		// Arrange
		const string body = """{"docs":[{"token":"secret-one"},{"token":"secret-two"}]}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotContain("secret-one");
		redacted.ShouldNotContain("secret-two");
	}

	[Fact]
	public void WithholdABodyThatIsNotWellFormedJson()
	{
		// Arrange - individual values cannot be located, so none of it can be shown
		const string body = """{"password":"hunter2", this is not json""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("password"));

		// Assert
		redacted.ShouldNotContain("hunter2");
		redacted.ShouldBe(LoggedBodyRedactor.UnparseableBody);
	}

	[Fact]
	public void RedactAValueThatIsNotInsideAnyPropertyAtAll()
	{
		// Arrange - a bare scalar has no property name to match against the allow list
		const string body = "1234567890";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("anything"));

		// Assert
		redacted.ShouldNotContain("1234567890");
	}

	#endregion

	#region Liveness - the log must still say something useful

	[Fact]
	public void EmitAnAllowListedValueVerbatim()
	{
		// Arrange
		const string body = """{"index":"orders"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("index"));

		// Assert
		redacted.ShouldBe("""{"index":"orders"}""");
	}

	[Fact]
	public void EmitAnAllowListedValueWhileRedactingItsNeighbour()
	{
		// Arrange
		const string body = """{"index":"orders","apiKey":"secret-key"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("index"));

		// Assert
		redacted.ShouldContain(""""index":"orders"""");
		redacted.ShouldNotContain("secret-key");
	}

	[Fact]
	public void EmitAnAllowListedSubtreeIntact()
	{
		// Arrange
		const string body = """{"shards":{"total":5,"failed":0}}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("shards"));

		// Assert
		redacted.ShouldBe("""{"shards":{"total":5,"failed":0}}""");
	}

	[Fact]
	public void MatchAllowListedNamesWithoutRegardToCase()
	{
		// Arrange
		const string body = """{"Index":"orders"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, Allow("index"));

		// Assert
		redacted.ShouldBe("""{"Index":"orders"}""");
	}

	[Fact]
	public void PreserveStructureAndPropertyNamesSoTheLogRemainsDiagnostic()
	{
		// Arrange
		const string body = """{"query":{"term":{"status":"active"}},"size":10}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert - names and shape survive even though every value is withheld
		redacted.ShouldContain("query");
		redacted.ShouldContain("term");
		redacted.ShouldContain("status");
		redacted.ShouldContain("size");
		Should.NotThrow(() => JsonDocument.Parse(redacted).Dispose());
	}

	[Fact]
	public void ProduceOutputForANonEmptyBody()
	{
		// Arrange - guards against a redactor that satisfies every safety assertion by emitting nothing
		const string body = """{"anything":"at all"}""";

		// Act
		var redacted = LoggedBodyRedactor.Redact(body, None);

		// Assert
		redacted.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion
}
