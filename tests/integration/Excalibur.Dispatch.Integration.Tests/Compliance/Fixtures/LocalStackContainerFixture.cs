// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
using Amazon.KeyManagementService;
using Amazon.Runtime;

using DotNet.Testcontainers.Builders;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

/// <summary>
/// Fixture for LocalStack container for AWS KMS integration tests.
/// </summary>
public class LocalStackContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;

	/// <summary>
	/// Gets the LocalStack service URL.
	/// </summary>
	public string ServiceUrl => _container?.GetConnectionString() ?? "http://localhost:4566";

	/// <summary>
	/// Gets an AWS KMS client configured for LocalStack.
	/// </summary>
	public AmazonKeyManagementServiceClient CreateKmsClient()
	{
		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonKeyManagementServiceConfig
		{
			ServiceURL = ServiceUrl,
			UseHttp = true,
			AuthenticationRegion = "us-east-1"
		};
		return new AmazonKeyManagementServiceClient(credentials, config);
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.WithName($"localstack-compliance-test-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "kms")
			.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(4566))
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(true);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(true);
		}
	}
}

/// <summary>
/// Collection definition for LocalStack/AWS integration tests.
/// </summary>
[CollectionDefinition(Name)]
public class LocalStackTestCollection : ICollectionFixture<LocalStackContainerFixture>
{
	public const string Name = "LocalStack";
}
