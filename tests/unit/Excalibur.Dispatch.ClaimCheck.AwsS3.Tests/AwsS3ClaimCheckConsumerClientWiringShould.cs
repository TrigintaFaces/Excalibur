// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.Dispatch.ClaimCheck.AwsS3;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.ClaimCheck.AwsS3.Tests;

/// <summary>
/// author≠impl WIRE lock (bg157e / Metz M12): a consumer's pre-registered <see cref="IAmazonS3"/> must be
/// honored end-to-end. <see cref="AwsS3ClaimCheckStore"/> has TWO public constructors — a self-constructing
/// one (owns its client) and an injected one (takes <see cref="IAmazonS3"/>) — so "the consumer's client
/// wins" is not <c>TryAddSingleton</c> alone: it also requires that <c>AddAwsS3ClaimCheck</c> registers the
/// store so the real DI container selects the INJECTED constructor and the resolved store actually ROUTES
/// operations to the consumer's client. This is the advertised-but-unwired / real-DI-resolve class.
/// </summary>
/// <remarks>
/// SAFETY/behavior: resolve through a REAL <see cref="ServiceProvider"/> (production registration path,
/// per S873) and assert the EMITTED behavior — the consumer's fake client receives <c>PutObjectAsync</c> —
/// not merely that a descriptor was registered. RED if DI selects the self-constructing constructor (the
/// fake would never be called). LIVENESS partner: with no consumer client registered, the provider still
/// resolves via the default <see cref="IAmazonS3"/> registration (the wiring is not vacuously dependent on
/// a consumer override).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsS3ClaimCheckConsumerClientWiringShould
{
	private const string Bucket = "dispatch-bucket";

	[Fact]
	public async Task RouteOperationsToTheConsumersPreRegisteredS3Client()
	{
		// Arrange — consumer pre-registers their own IAmazonS3 BEFORE AddAwsS3ClaimCheck (the AWS-SDK
		// AddAWSService<IAmazonS3>() scenario). TryAddSingleton must let it win, and the store must resolve it.
		var consumerClient = A.Fake<IAmazonS3>();
		A.CallTo(() => consumerClient.PutObjectAsync(A<PutObjectRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new PutObjectResponse()));

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(consumerClient);
		_ = services.AddAwsS3ClaimCheck(options => options.BucketName = Bucket);

		await using var provider = services.BuildServiceProvider();

		// Act — resolve through the REAL container and drive a store operation.
		var claimCheck = provider.GetRequiredService<IClaimCheckProvider>();
		_ = await claimCheck.StoreAsync([1, 2, 3], CancellationToken.None).ConfigureAwait(false);

		// Assert — the CONSUMER's client received the operation (injected ctor selected + routed end-to-end).
		A.CallTo(() => consumerClient.PutObjectAsync(
				A<PutObjectRequest>.That.Matches(r => r.BucketName == Bucket),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ResolveTheProvider_WhenNoConsumerClientIsRegistered()
	{
		// LIVENESS — the default IAmazonS3 registration lets the store resolve even without a consumer
		// override, so the wiring is not vacuously dependent on a consumer-supplied client.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAwsS3ClaimCheck(options =>
		{
			options.BucketName = Bucket;
			// No consumer client here, so the default IAmazonS3 factory constructs a real client — give it a
			// region so construction succeeds (no network call is made; the store is only resolved, not driven).
			options.Region = "us-east-1";
		});

		using var provider = services.BuildServiceProvider();

		var claimCheck = provider.GetRequiredService<IClaimCheckProvider>();

		_ = claimCheck.ShouldBeOfType<AwsS3ClaimCheckStore>();
	}
}
