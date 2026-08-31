// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using DotNet.Testcontainers.Containers;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.ClaimCheck;

/// <summary>
/// Fixture for a LocalStack S3 container backing the real-infrastructure
/// <see cref="Excalibur.Dispatch.ClaimCheck.AwsS3.AwsS3ClaimCheckStore"/> conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// The shared <c>LocalStackContainerFixture</c> under <c>Compliance/Fixtures</c> only enables the
/// <c>kms</c> service, so it cannot serve S3 requests. This fixture is a separate, dedicated
/// container enabling only <c>s3</c>, owned by the ClaimCheck folder rather than editing the shared
/// fixture (which other suites depend on for KMS).
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/> without overriding <c>AllowGracefulDegradation</c>, so
/// a container that cannot start throws instead of silently degrading -- the real-infra claim-check
/// conformance suite is never skipped.
/// </para>
/// </remarks>
public sealed class AwsS3ClaimCheckContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private IAmazonS3? _s3Client;

	/// <summary>
	/// Gets the S3 bucket name provisioned for this fixture's lifetime. Every conformance test in the
	/// collection shares this bucket; claim-check IDs are GUID-based, so keys never collide.
	/// </summary>
	public string BucketName { get; } = $"claimcheck-{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the S3 client pointed at the LocalStack container, built with the SDK's default
	/// configuration (only the LocalStack <c>ServiceURL</c>, path-style addressing, and test
	/// credentials are supplied).
	/// </summary>
	public IAmazonS3 S3Client => _s3Client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-claimcheck-s3-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "s3")
			.WithEnvironment("EAGER_SERVICE_LOADING", "1")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var config = new AmazonS3Config
		{
			ServiceURL = _container.GetConnectionString(),
			ForcePathStyle = true,
			UseHttp = true,
			Timeout = TimeSpan.FromSeconds(30),
			MaxErrorRetry = 1
		};

		_s3Client = new AmazonS3Client("test", "test", config);
		_ = await _s3Client.PutBucketAsync(BucketName, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		_s3Client?.Dispose();
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Collection definition for the real-infrastructure AWS S3 claim-check conformance tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AwsS3ClaimCheckTestCollection : ICollectionFixture<AwsS3ClaimCheckContainerFixture>
{
	public const string Name = "ClaimCheck-AwsS3";
}
