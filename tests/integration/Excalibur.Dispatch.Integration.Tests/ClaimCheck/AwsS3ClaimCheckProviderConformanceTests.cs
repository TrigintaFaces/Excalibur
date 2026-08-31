// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ClaimCheck.AwsS3;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.ClaimCheck;

/// <summary>
/// Runs the shared claim-check conformance kit against the REAL <see cref="AwsS3ClaimCheckStore"/> on
/// a LocalStack S3 container.
/// </summary>
/// <remarks>
/// <para>
/// Until this class existed, the only type deriving <see cref="ClaimCheckProviderConformanceTestKit"/>
/// was an in-memory dictionary reference implementation, so every arm ran against the one
/// implementation with no blob storage in it. A dictionary round-trips whatever object it was handed;
/// this store does not -- it serializes the payload into an S3 <c>PutObjectRequest</c>, and reads it
/// back through a real <c>GetObjectAsync</c> call, translating S3's own not-found/error shapes into the
/// contract's exceptions. Only a real S3-compatible server exercises that translation.
/// </para>
/// <para>
/// The expiry arms bind the retention contract to this store. S3 has no per-object time-to-live -- its
/// lifecycle rules are bucket-wide and delete on a daily schedule -- so
/// <see cref="AwsS3ClaimCheckStore.RetrieveAsync"/> enforces expiry itself against
/// <see cref="ClaimCheckReference.ExpiresAt"/>, and a zero retention period leaves that unset so the
/// payload never expires.
/// </para>
/// </remarks>
[Collection(AwsS3ClaimCheckTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class AwsS3ClaimCheckProviderConformanceTests : ClaimCheckProviderConformanceTestKit
{
	private readonly AwsS3ClaimCheckContainerFixture _fixture;

	public AwsS3ClaimCheckProviderConformanceTests(AwsS3ClaimCheckContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProvider() => CreateProvider(TimeSpan.Zero);

	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProviderWithTtl(TimeSpan ttl) => CreateProvider(ttl);

	private IClaimCheckProvider CreateProvider(TimeSpan ttl)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a LocalStack S3 container must be available - real-infra claim-check conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working.");

		var s3Options = Microsoft.Extensions.Options.Options.Create(new AwsS3ClaimCheckOptions
		{
			BucketName = _fixture.BucketName
		});

		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions
		{
			PayloadThreshold = 256 * 1024,
			DefaultTtl = ttl,
			EnableCompression = false
		});

		return new AwsS3ClaimCheckStore(
			_fixture.S3Client,
			s3Options,
			claimCheckOptions,
			NullLogger<AwsS3ClaimCheckStore>.Instance);
	}

	#region Store Tests

	[Fact]
	public Task StoreAsync_NullPayload_ShouldThrowArgumentNullException_Test() =>
		StoreAsync_NullPayload_ShouldThrowArgumentNullException();

	[Fact]
	public Task StoreAsync_ShouldPopulateReferenceMetadata_Test() =>
		StoreAsync_ShouldPopulateReferenceMetadata();

	[Fact]
	public Task StoreAsync_WithMetadata_ShouldPreserveMetadata_Test() =>
		StoreAsync_WithMetadata_ShouldPreserveMetadata();

	#endregion

	#region Retrieve Tests

	[Fact]
	public Task RetrieveAsync_NullReference_ShouldThrowArgumentNullException_Test() =>
		RetrieveAsync_NullReference_ShouldThrowArgumentNullException();

	[Fact]
	public Task RetrieveAsync_NonExistent_ShouldThrowKeyNotFoundException_Test() =>
		RetrieveAsync_NonExistent_ShouldThrowKeyNotFoundException();

	#endregion

	#region Delete Tests

	[Fact]
	public Task DeleteAsync_NullReference_ShouldThrowArgumentNullException_Test() =>
		DeleteAsync_NullReference_ShouldThrowArgumentNullException();

	[Fact]
	public Task DeleteAsync_ExistingPayload_ShouldReturnTrue_Test() =>
		DeleteAsync_ExistingPayload_ShouldReturnTrue();

	[Fact]
	public Task DeleteAsync_NonExistent_ShouldReturnFalse_Test() =>
		DeleteAsync_NonExistent_ShouldReturnFalse();

	#endregion

	#region ShouldUseClaimCheck Tests (SYNC!)

	[Fact]
	public Task ShouldUseClaimCheck_NullPayload_ShouldThrowArgumentNullException_Test() =>
		ShouldUseClaimCheck_NullPayload_ShouldThrowArgumentNullException();

	[Fact]
	public Task ShouldUseClaimCheck_BelowThreshold_ShouldReturnFalse_Test() =>
		ShouldUseClaimCheck_BelowThreshold_ShouldReturnFalse();

	[Fact]
	public Task ShouldUseClaimCheck_AboveThreshold_ShouldReturnTrue_Test() =>
		ShouldUseClaimCheck_AboveThreshold_ShouldReturnTrue();

	#endregion

	#region Round-Trip Tests

	[Fact]
	public Task RoundTrip_StoreRetrieve_ShouldReturnOriginalPayload_Test() =>
		RoundTrip_StoreRetrieve_ShouldReturnOriginalPayload();

	[Fact]
	public Task RoundTrip_TextData_ShouldPreserveContent_Test() =>
		RoundTrip_TextData_ShouldPreserveContent();

	#endregion

	#region Expiration Tests

	[Fact]
	public Task RetrieveAsync_ExpiredPayload_ShouldThrowKeyNotFoundException_Test() =>
		RetrieveAsync_ExpiredPayload_ShouldThrowKeyNotFoundException();

	[Fact]
	public Task RetrieveAsync_ZeroTtl_ShouldNotExpire_Test() =>
		RetrieveAsync_ZeroTtl_ShouldNotExpire();

	#endregion

	/// <summary>
	/// Wires the kit's cleanup lifecycle hook so the wiring guard sees it as executed. The base
	/// implementation is a no-op (<see cref="Task.CompletedTask"/>), but the guard enumerates every
	/// declared protected/public virtual no-arg <see cref="Task"/> member as an "arm" -- including this
	/// one -- so it must be wired like any other, or the guard reports it unwired.
	/// </summary>

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
