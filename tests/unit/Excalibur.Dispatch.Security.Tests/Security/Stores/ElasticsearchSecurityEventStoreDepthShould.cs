// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Depth tests for <see cref="ElasticsearchSecurityEventStore"/>.
/// Covers constructor edge cases, type assertions, and interface validation.
/// Note: The constructor has a known bug (Content-Type added to DefaultRequestHeaders)
/// which prevents testing StoreEventsAsync/QueryEventsAsync without reflection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class ElasticsearchSecurityEventStoreDepthShould
{
	[Fact]
	public void HaveStoreEventsAsyncMethod()
	{
		var method = typeof(ElasticsearchSecurityEventStore).GetMethod(
			"StoreEventsAsync",
			BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
		method.GetParameters().Length.ShouldBe(2);
		method.GetParameters()[0].ParameterType.ShouldBe(typeof(IEnumerable<SecurityEvent>));
		method.GetParameters()[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveQueryEventsAsyncMethod()
	{
		var method = typeof(ElasticsearchSecurityEventStore).GetMethod(
			"QueryEventsAsync",
			BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<IEnumerable<SecurityEvent>>));
		method.GetParameters().Length.ShouldBe(2);
		method.GetParameters()[0].ParameterType.ShouldBe(typeof(SecurityEventQuery));
		method.GetParameters()[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveDisposeMethod()
	{
		var method = typeof(ElasticsearchSecurityEventStore).GetMethod(
			"Dispose",
			BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(void));
	}

	[Fact]
	public void Constructor_ThrowsUriFormatException_WhenConnectionStringIsEmpty()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Elasticsearch"] = "",
			})
			.Build();

		// Empty string passes null check but causes UriFormatException when creating URI
		Should.Throw<UriFormatException>(() =>
			new ElasticsearchSecurityEventStore(
				NullLogger<ElasticsearchSecurityEventStore>.Instance,
				configuration,
				new HttpClient()));
	}

	[Fact]
	public void HaveValidateAndCountEventsMethod()
	{
		var method = typeof(ElasticsearchSecurityEventStore).GetMethod(
			"ValidateAndCountEvents",
			BindingFlags.NonPublic | BindingFlags.Instance);

		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(int));
	}

	[Fact]
	public void HaveValidateQueryParametersMethod()
	{
		var method = typeof(ElasticsearchSecurityEventStore).GetMethod(
			"ValidateQueryParameters",
			BindingFlags.NonPublic | BindingFlags.Static);

		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveSemaphoreSlimField()
	{
		var field = typeof(ElasticsearchSecurityEventStore).GetField(
			"_semaphore",
			BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field!.FieldType.ShouldBe(typeof(SemaphoreSlim));
	}

	[Fact]
	public void HaveConnectionStringField()
	{
		var field = typeof(ElasticsearchSecurityEventStore).GetField(
			"_connectionString",
			BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field!.FieldType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveIndexPrefixField()
	{
		var field = typeof(ElasticsearchSecurityEventStore).GetField(
			"_indexPrefix",
			BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field!.FieldType.ShouldBe(typeof(string));
	}
}
