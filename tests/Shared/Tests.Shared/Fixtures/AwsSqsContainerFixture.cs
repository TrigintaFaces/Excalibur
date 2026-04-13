// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Amazon.Runtime;
using Amazon.SQS;

using Testcontainers.LocalStack;

using Tests.Shared.Infrastructure;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for AWS SQS integration tests using LocalStack.
/// </summary>
/// <remarks>
/// <para>
/// Provides a shared LocalStack container with SQS service enabled.
/// All AWS SQS integration tests should share this fixture via
/// <c>[Collection(ContainerCollections.AwsSqs)]</c> rather than creating
/// per-class containers, to reduce Docker resource contention and prevent
/// test host hangs from undisposed static containers.
/// </para>
/// <para>
/// The fixture exposes a pre-configured <see cref="AmazonSQSClient"/> that
/// points to the LocalStack container's SQS endpoint with test credentials.
/// </para>
/// </remarks>
#pragma warning disable CA1001 // Disposal is handled by ContainerFixtureBase.DisposeAsync -> DisposeContainerAsync
public sealed class AwsSqsContainerFixture : ContainerFixtureBase
#pragma warning restore CA1001
{
	/// <summary>
	/// Default LocalStack Docker image. Uses latest for broadest SQS compatibility.
	/// </summary>
	public const string DefaultImage = "localstack/localstack:latest";

	private LocalStackContainer? _container;
	private AmazonSQSClient? _sqsClient;

	/// <summary>
	/// Gets the pre-configured SQS client connected to the LocalStack container.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when accessed before the container has been initialized.
	/// Tests should check <see cref="ContainerFixtureBase.DockerAvailable"/> first.
	/// </exception>
	public AmazonSQSClient SqsClient => _sqsClient
		?? throw new InvalidOperationException("SQS client is not available. Check DockerAvailable before accessing.");

	/// <summary>
	/// Gets the LocalStack connection string (endpoint URL).
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when accessed before the container has been initialized.
	/// </exception>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container is not available. Check DockerAvailable before accessing.");

	/// <inheritdoc/>
	/// <remarks>
	/// LocalStack is optional — not all CI environments have Docker or pull capacity for it.
	/// When unavailable, tests using this fixture skip gracefully.
	/// </remarks>
	protected override bool AllowGracefulDegradation => true;

	/// <inheritdoc/>
	/// <remarks>
	/// LocalStack with SQS typically starts within 60 seconds, but CI environments
	/// may be slower. Use the base default timeout with CI scaling.
	/// </remarks>
	protected override TimeSpan ContainerStartTimeout => TestTimeouts.Scale(TimeSpan.FromMinutes(2));

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage(DefaultImage)
			.WithName($"localstack-sqs-test-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "sqs")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonSQSConfig
		{
			ServiceURL = _container.GetConnectionString(),
		};
		_sqsClient = new AmazonSQSClient(credentials, config);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		_sqsClient?.Dispose();

		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
