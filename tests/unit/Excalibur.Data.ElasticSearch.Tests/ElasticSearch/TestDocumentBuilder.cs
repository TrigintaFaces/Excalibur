// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;
using System.Text.Json.Serialization;

using Bogus;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.Builders;

/// <summary>
/// Builder for creating test documents with realistic data.
/// </summary>
public class TestDocumentBuilder<TDocument> where TDocument : class, new()
{
	private readonly Faker<TDocument> _faker;
	private readonly List<Action<Faker, TDocument>> _rules;

	/// <summary>
	/// Initializes a new instance of the <see cref="TestDocumentBuilder{TDocument}" /> class.
	/// </summary>
	public TestDocumentBuilder()
	{
		_faker = new Faker<TDocument>();
		_rules = [];

		// Set default locale
		_faker.Locale = "en";

		// Configure default rules based on property names
		ConfigureDefaultRules();
	}

	/// <summary>
	/// Sets a specific value for a property.
	/// </summary>
	/// <typeparam name="TProperty"> The property type. </typeparam>
	/// <param name="propertyExpression"> The property expression. </param>
	/// <param name="value"> The value to set. </param>
	/// <returns> The builder instance. </returns>
	public TestDocumentBuilder<TDocument> With<TProperty>(
		Expression<Func<TDocument, TProperty>> propertyExpression,
		TProperty value)
	{
		_ = _faker.RuleFor(propertyExpression, value);
		return this;
	}

	/// <summary>
	/// Sets a rule for generating a property value.
	/// </summary>
	/// <typeparam name="TProperty"> The property type. </typeparam>
	/// <param name="propertyExpression"> The property expression. </param>
	/// <param name="setter"> The value generator. </param>
	/// <returns> The builder instance. </returns>
	public TestDocumentBuilder<TDocument> WithRule<TProperty>(
		Expression<Func<TDocument, TProperty>> propertyExpression,
		Func<Faker, TProperty> setter)
	{
		_ = _faker.RuleFor(propertyExpression, setter);
		return this;
	}

	/// <summary>
	/// Sets the seed for deterministic data generation.
	/// </summary>
	/// <param name="seed"> The seed value. </param>
	/// <returns> The builder instance. </returns>
	public TestDocumentBuilder<TDocument> WithSeed(int seed)
	{
		_ = _faker.UseSeed(seed);
		return this;
	}

	/// <summary>
	/// Sets the locale for data generation.
	/// </summary>
	/// <param name="locale"> The locale code. </param>
	/// <returns> The builder instance. </returns>
	public TestDocumentBuilder<TDocument> WithLocale(string locale)
	{
		_faker.Locale = locale;
		return this;
	}

	/// <summary>
	/// Adds a custom rule to be applied after generation.
	/// </summary>
	/// <param name="rule"> The rule to apply. </param>
	/// <returns> The builder instance. </returns>
	public TestDocumentBuilder<TDocument> WithPostRule(Action<Faker, TDocument> rule)
	{
		_rules.Add(rule);
		return this;
	}

	/// <summary>
	/// Builds a single test document.
	/// </summary>
	/// <returns> A test document. </returns>
	public TDocument Build()
	{
		var document = _faker.Generate();

		// Apply post-generation rules
		var faker = new Faker();
		foreach (var rule in _rules)
		{
			rule(faker, document);
		}

		return document;
	}

	/// <summary>
	/// Builds multiple test documents.
	/// </summary>
	/// <param name="count"> The number of documents to generate. </param>
	/// <returns> A collection of test documents. </returns>
	public IList<TDocument> BuildMany(int count)
	{
		var documents = new List<TDocument>(count);

		for (var i = 0; i < count; i++)
		{
			documents.Add(Build());
		}

		return documents;
	}

	/// <summary>
	/// Configures default rules based on common property names.
	/// </summary>
	private void ConfigureDefaultRules()
	{
		var properties = typeof(TDocument).GetProperties()
			.Where(static p => p.CanWrite && p.GetIndexParameters().Length == 0);

		foreach (var property in properties)
		{
			var propertyName = property.Name.ToUpperInvariant();

			if (property.PropertyType == typeof(string))
			{
				if (propertyName.Contains("id", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Guid().ToString());
				}
				else if (propertyName.Contains("name", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Name.FullName());
				}
				else if (propertyName.Contains("email", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Internet.Email());
				}
				else if (propertyName.Contains("phone", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Phone.PhoneNumber());
				}
				else if (propertyName.Contains("address", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Address.FullAddress());
				}
				else if (propertyName.Contains("description", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("content", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Lorem.Paragraph());
				}
				else if (propertyName.Contains("title", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Lorem.Sentence());
				}
				else if (propertyName.Contains("url", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("link", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Internet.Url());
				}
				else if (propertyName.Contains("tag", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("category", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Commerce.Categories(1).First());
				}
			}
			else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
			{
				if (propertyName.Contains("age", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Int(18, 80));
				}
				else if (propertyName.Contains("count", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("quantity", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Int(0, 100));
				}
				else
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Int(1, 1000));
				}
			}
			else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
			{
				if (propertyName.Contains("price", StringComparison.OrdinalIgnoreCase) ||
					propertyName.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
					propertyName.Contains("amount", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Decimal(0.01m, 10000m));
				}
				else if (propertyName.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("percentage", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Random.Decimal(0m, 100m));
				}
			}
			else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
			{
				if (propertyName.Contains("created", StringComparison.OrdinalIgnoreCase) ||
					propertyName.Contains("start", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Date.Past());
				}
				else if (propertyName.Contains("updated", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("modified", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Date.Recent());
				}
				else if (propertyName.Contains("end", StringComparison.OrdinalIgnoreCase) ||
						 propertyName.Contains("expire", StringComparison.OrdinalIgnoreCase))
				{
					_ = _faker.RuleFor(property.Name, static f => f.Date.Future());
				}
			}
			else if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
			{
				_ = _faker.RuleFor(property.Name, static f => f.Random.Bool());
			}
			else if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
			{
				_ = _faker.RuleFor(property.Name, static f => f.Random.Guid());
			}
		}
	}
}

/// <summary>
/// Sample test document for testing.
/// </summary>
public class TestDocument
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TestDocument" /> class.
	/// </summary>
	public TestDocument()
	{
		Id = string.Empty;
		Title = string.Empty;
		Content = string.Empty;
		Author = string.Empty;
		Updated = null;
		Rating = null;
	}

	/// <summary>
	/// Gets or sets the document ID.
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; }

	/// <summary>
	/// Gets or sets the title.
	/// </summary>
	[JsonPropertyName("title")]
	public string Title { get; set; }

	/// <summary>
	/// Gets or sets the content.
	/// </summary>
	[JsonPropertyName("content")]
	public string Content { get; set; }

	/// <summary>
	/// Gets or sets the author name.
	/// </summary>
	[JsonPropertyName("author")]
	public string Author { get; set; }

	/// <summary>
	/// Gets the tags.
	/// </summary>
	[JsonPropertyName("tags")]
	public IList<string> Tags { get; } = [];

	/// <summary>
	/// Gets or sets the created date.
	/// </summary>
	[JsonPropertyName("created")]
	public DateTime Created { get; set; }

	/// <summary>
	/// Gets or sets the updated date.
	/// </summary>
	[JsonPropertyName("updated")]
	public DateTime? Updated { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the document is published.
	/// </summary>
	[JsonPropertyName("isPublished")]
	public bool IsPublished { get; set; }

	/// <summary>
	/// Gets or sets the view count.
	/// </summary>
	[JsonPropertyName("viewCount")]
	public int ViewCount { get; set; }

	/// <summary>
	/// Gets or sets the rating.
	/// </summary>
	[JsonPropertyName("rating")]
	public decimal? Rating { get; set; }

	/// <summary>
	/// Gets the metadata.
	/// </summary>
	[JsonPropertyName("metadata")]
	public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Factory for creating test documents.
/// </summary>
public static class TestDocumentFactory
{
	/// <summary>
	/// Creates a simple test document.
	/// </summary>
	/// <param name="id"> Optional document ID. </param>
	/// <returns> A test document. </returns>
	public static TestDocument CreateSimple(string? id = null) =>
		new TestDocumentBuilder<TestDocument>()
			.With(static d => d.Id, id ?? Guid.NewGuid().ToString())
			.With(static d => d.IsPublished, true)
			.Build();

	/// <summary>
	/// Creates a collection of test documents.
	/// </summary>
	/// <param name="count"> The number of documents to create. </param>
	/// <param name="seed"> Optional seed for deterministic generation. </param>
	/// <returns> A collection of test documents. </returns>
	public static IList<TestDocument> CreateMany(int count, int? seed = null)
	{
		var builder = new TestDocumentBuilder<TestDocument>();

		if (seed.HasValue)
		{
			_ = builder.WithSeed(seed.Value);
		}

		return builder.BuildMany(count);
	}

	/// <summary>
	/// Creates a test document with specific tags.
	/// </summary>
	/// <param name="tags"> The tags to assign. </param>
	/// <returns> A test document. </returns>
	public static TestDocument CreateWithTags(params string[] tags) =>
		new TestDocumentBuilder<TestDocument>()
			.With(static d => d.Tags, [.. tags])
			.Build();

	/// <summary>
	/// Creates an unpublished test document.
	/// </summary>
	/// <returns> A test document. </returns>
	public static TestDocument CreateUnpublished() =>
		new TestDocumentBuilder<TestDocument>()
			.With(static d => d.IsPublished, false)
			.With(static d => d.ViewCount, 0)
			.Build();
}
