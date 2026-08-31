// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch.IndexManagement;

using OpenSearch.Client;

namespace Excalibur.EventSourcing.Tests.OpenSearch;

/// <summary>
/// Locks the index management registration. The four managers were advertised as
/// DI-resolvable while nothing in the package registered them, so a consumer following
/// the documentation got an unresolvable service. Each arm below goes RED if its
/// registration is removed from AddOpenSearchIndexManagement.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "OpenSearch")]
public sealed class OpenSearchIndexManagementRegistrationShould
{
	private static ServiceProvider BuildWithClient()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOpenSearchServices("https://localhost:9200");
		_ = services.AddOpenSearchIndexManagement();
		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
		});
	}

	[Fact]
	public void ResolveTheLifecycleManager()
	{
		using var provider = BuildWithClient();
		provider.GetRequiredService<IIndexLifecycleManager>().ShouldNotBeNull();
	}

	[Fact]
	public void ResolveTheTemplateManager()
	{
		using var provider = BuildWithClient();
		provider.GetRequiredService<IIndexTemplateManager>().ShouldNotBeNull();
	}

	[Fact]
	public void ResolveTheOperationsManager()
	{
		using var provider = BuildWithClient();
		provider.GetRequiredService<IIndexOperationsManager>().ShouldNotBeNull();
	}

	[Fact]
	public void ResolveTheAliasManager()
	{
		using var provider = BuildWithClient();
		provider.GetRequiredService<IIndexAliasManager>().ShouldNotBeNull();
	}

	/// <summary>
	/// The package's own entry points register the concrete <see cref="OpenSearchClient"/>,
	/// never the <see cref="IOpenSearchClient"/> interface. A registration that asked only
	/// for the interface would be advertised and unresolvable on the one path the package
	/// itself produces, so this arm fails if the factory stops accepting both shapes.
	/// </summary>
	[Fact]
	public void AcceptAnInterfaceOnlyClientRegistration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var settings = new ConnectionSettings(new Uri("https://localhost:9200"));
		_ = services.AddSingleton<IOpenSearchClient>(new OpenSearchClient(settings));
		_ = services.AddOpenSearchIndexManagement();

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IIndexAliasManager>().ShouldNotBeNull();
	}

	/// <summary>
	/// Resolving with no client must say which registration is missing rather than
	/// silently constructing a client aimed at a default local address.
	/// </summary>
	[Fact]
	public void ThrowNamingTheMissingClientRegistration()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOpenSearchIndexManagement();

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<IIndexLifecycleManager>());

		ex.Message.ShouldContain("AddOpenSearchServices");
	}
}
