// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryClaimCheckProvider"/> validating IClaimCheckProvider contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryClaimCheckProvider uses instance-level ConcurrentDictionary with no static state,
/// so no special isolation is required beyond using fresh store instances.
/// </para>
/// <para>
/// <strong>ENTERPRISE INTEGRATION PATTERN:</strong> IClaimCheckProvider implements the Claim Check
/// pattern for handling large message payloads in distributed messaging systems.
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>StoreAsync populates ClaimCheckReference metadata (Id, BlobName, Location, Size, StoredAt)</description></item>
/// <item><description>StoreAsync throws ArgumentNullException on null payload</description></item>
/// <item><description>RetrieveAsync throws ArgumentNullException on null reference</description></item>
/// <item><description>RetrieveAsync throws KeyNotFoundException for non-existent payload</description></item>
/// <item><description>RetrieveAsync throws InvalidOperationException for expired payload</description></item>
/// <item><description>DeleteAsync returns true for existing, false for non-existent</description></item>
/// <item><description>ShouldUseClaimCheck (SYNC!) returns based on PayloadThreshold</description></item>
/// <item><description>Round-trip: Store → Retrieve returns original payload</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public class InMemoryClaimCheckProviderConformanceTests : ClaimCheckProviderConformanceTestKit
{
	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProvider()
	{
		var options = Options.Create(new ClaimCheckOptions
		{
			PayloadThreshold = 256 * 1024, // 256KB default
			DefaultTtl = TimeSpan.Zero, // Disable TTL for most tests
			EnableCompression = false // Disable compression for predictable testing
		});
		return new InMemoryClaimCheckProvider(options);
	}

	/// <inheritdoc />
	protected override IClaimCheckProvider CreateProviderWithTtl(TimeSpan ttl)
	{
		var options = Options.Create(new ClaimCheckOptions
		{
			PayloadThreshold = 256 * 1024,
			DefaultTtl = ttl,
			EnableCompression = false
		});
		return new InMemoryClaimCheckProvider(options);
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

	#endregion Store Tests

	#region Retrieve Tests

	[Fact]
	public Task RetrieveAsync_NullReference_ShouldThrowArgumentNullException_Test() =>
		RetrieveAsync_NullReference_ShouldThrowArgumentNullException();

	[Fact]
	public Task RetrieveAsync_NonExistent_ShouldThrowKeyNotFoundException_Test() =>
		RetrieveAsync_NonExistent_ShouldThrowKeyNotFoundException();

	#endregion Retrieve Tests

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

	#endregion Delete Tests

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

	#endregion ShouldUseClaimCheck Tests (SYNC!)

	#region Round-Trip Tests

	[Fact]
	public Task RoundTrip_StoreRetrieve_ShouldReturnOriginalPayload_Test() =>
		RoundTrip_StoreRetrieve_ShouldReturnOriginalPayload();

	[Fact]
	public Task RoundTrip_TextData_ShouldPreserveContent_Test() =>
		RoundTrip_TextData_ShouldPreserveContent();

	#endregion Round-Trip Tests

	#region Expiration Tests

	[Fact]
	public Task RetrieveAsync_ExpiredPayload_ShouldThrowInvalidOperationException_Test() =>
		RetrieveAsync_ExpiredPayload_ShouldThrowInvalidOperationException();

	#endregion Expiration Tests
}
