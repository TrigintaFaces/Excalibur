// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.ClaimCheck;

/// <summary>
/// Runs the shared claim-check conformance kit against the REAL <see cref="GcsClaimCheckStore"/> on a
/// Cloud Storage emulator container.
/// </summary>
/// <remarks>
/// <para>
/// Until this class existed this store had no conformance deriver at all, which is why a defect the kit
/// already detects went unobserved here: <c>DeleteAsync</c> returned <see langword="true"/> whether or
/// not an object had been there. The kit's <c>DeleteAsync_NonExistent_ShouldReturnFalse</c> arm is the
/// same arm that caught it on the sibling S3 store; the arm was never wrong, it simply never ran against
/// this provider.
/// </para>
/// <para>
/// The emulator is load-bearing rather than convenience. Cloud Storage's delete is idempotent and
/// silent, so the observation has to come from a separate metadata request, and the store's answer
/// depends on translating the SDK's not-found response into "absent" while letting every other status
/// remain a fault. A substituted seam cannot exercise that translation — only a server that really
/// returns 404 can.
/// </para>
/// <para>
/// The store is constructed through its public <c>StorageClient</c> constructor rather than the internal
/// seam constructor, deliberately: that path builds the real adapter, so the adapter is part of what is
/// under test.
/// </para>
/// </remarks>
[Collection(GcsClaimCheckTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class GcsClaimCheckProviderConformanceTests : ClaimCheckProviderConformanceTestKit
{
	private readonly GcsClaimCheckContainerFixture _fixture;

	public GcsClaimCheckProviderConformanceTests(GcsClaimCheckContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProvider() => CreateProvider(TimeSpan.Zero);

	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProviderWithTtl(TimeSpan ttl) => CreateProvider(ttl);

	private IClaimCheckProvider CreateProvider(TimeSpan ttl)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Cloud Storage emulator container must be available - real-infra claim-check conformance is "
			+ "never skipped, because an arm that passes by being skipped is indistinguishable from one "
			+ "that passed by working.");

		var gcsOptions = Microsoft.Extensions.Options.Options.Create(new GcsClaimCheckOptions
		{
			BucketName = _fixture.BucketName
		});

		var claimCheckOptions = Microsoft.Extensions.Options.Options.Create(new ClaimCheckOptions
		{
			PayloadThreshold = 256 * 1024,
			DefaultTtl = ttl,
			EnableCompression = false
		});

		return new GcsClaimCheckStore(
			_fixture.StorageClient,
			gcsOptions,
			claimCheckOptions,
			NullLogger<GcsClaimCheckStore>.Instance);
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

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
