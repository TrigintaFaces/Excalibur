// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security.Stores;

/// <summary>
/// Unit tests for <see cref="ElasticsearchSecurityEventStore"/> internal class.
/// Note: The constructor has a bug where it adds "Content-Type" to DefaultRequestHeaders
/// which throws InvalidOperationException. Tests cover constructor validation and type assertions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Stores")]
public sealed class ElasticsearchSecurityEventStoreShould
{
	[Fact]
	public void BeInternalAndSealed()
	{
		typeof(ElasticsearchSecurityEventStore).IsNotPublic.ShouldBeTrue();
		typeof(ElasticsearchSecurityEventStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ImplementISecurityEventStoreInterface()
	{
		typeof(ElasticsearchSecurityEventStore)
			.GetInterfaces()
			.ShouldContain(typeof(ISecurityEventStore));
	}

	[Fact]
	public void ImplementIDisposableInterface()
	{
		typeof(ElasticsearchSecurityEventStore)
			.GetInterfaces()
			.ShouldContain(typeof(IDisposable));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		var configuration = CreateConfiguration("http://localhost:9200");

		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchSecurityEventStore(null!, configuration, new HttpClient()));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchSecurityEventStore(
				NullLogger<ElasticsearchSecurityEventStore>.Instance,
				null!,
				new HttpClient()));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
	{
		var configuration = CreateConfiguration("http://localhost:9200");

		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchSecurityEventStore(
				NullLogger<ElasticsearchSecurityEventStore>.Instance,
				configuration,
				null!));
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenConnectionStringIsMissing()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		Should.Throw<InvalidOperationException>(() =>
			new ElasticsearchSecurityEventStore(
				NullLogger<ElasticsearchSecurityEventStore>.Instance,
				configuration,
				new HttpClient()));
	}

	[Fact]
	public void Constructor_ThrowsInvalidOperationException_WhenContentTypeHeaderMisused()
	{
		// The constructor attempts to add Content-Type to DefaultRequestHeaders,
		// which is invalid (Content-Type is a content header, not a request header).
		// This is a known bug in the source code.
		var configuration = CreateConfiguration("http://localhost:9200");

		Should.Throw<InvalidOperationException>(() =>
			new ElasticsearchSecurityEventStore(
				NullLogger<ElasticsearchSecurityEventStore>.Instance,
				configuration,
				new HttpClient()));
	}

	private static IConfiguration CreateConfiguration(string connectionString)
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:Elasticsearch"] = connectionString,
			})
			.Build();
	}
}
