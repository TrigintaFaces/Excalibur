// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="EncryptionProviderRegistry"/> validating IEncryptionProviderRegistry contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// EncryptionProviderRegistry is the standard implementation that manages multiple encryption providers
/// with primary/legacy support for zero-downtime migration. It has a parameterless constructor and
/// uses ConcurrentDictionary + Lock for thread safety.
/// </para>
/// <para>
/// <strong>REGISTRY PATTERN:</strong> IEncryptionProviderRegistry is a multi-provider management interface
/// with 7 methods - the most since ServiceRegistry (Sprint 150).
/// </para>
/// <para>
/// Key behaviors verified:
/// <list type="bullet">
/// <item><description>Register adds provider with unique ID, throws on null/duplicate</description></item>
/// <item><description>GetProvider returns provider or null, case-insensitive, throws on null</description></item>
/// <item><description>GetPrimary throws if no primary, returns primary after SetPrimary</description></item>
/// <item><description>SetPrimary throws on null or unregistered providerId</description></item>
/// <item><description>GetLegacyProviders returns empty list initially</description></item>
/// <item><description>GetAll returns all registered providers</description></item>
/// <item><description>FindDecryptionProvider throws on null encryptedData</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "REGISTRY")]
public sealed class EncryptionProviderRegistryConformanceTests : EncryptionProviderRegistryConformanceTestKit
{
	/// <inheritdoc />
	protected override IEncryptionProviderRegistry CreateRegistry() =>
		new EncryptionProviderRegistry();

	/// <inheritdoc />
	protected override IEncryptionProvider CreateMockProvider() =>
		new StubEncryptionProvider();

	#region Register Method Tests

	[Fact]
	public void Register_ShouldSucceed_Test() =>
		Register_ShouldSucceed();

	[Fact]
	public void Register_NullProviderId_ShouldThrowArgumentNullException_Test() =>
		Register_NullProviderId_ShouldThrowArgumentNullException();

	[Fact]
	public void Register_NullProvider_ShouldThrowArgumentNullException_Test() =>
		Register_NullProvider_ShouldThrowArgumentNullException();

	[Fact]
	public void Register_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
		Register_DuplicateId_ShouldThrowInvalidOperationException();

	#endregion Register Method Tests

	#region GetProvider Method Tests

	[Fact]
	public void GetProvider_Registered_ShouldReturnProvider_Test() =>
		GetProvider_Registered_ShouldReturnProvider();

	[Fact]
	public void GetProvider_Unknown_ShouldReturnNull_Test() =>
		GetProvider_Unknown_ShouldReturnNull();

	[Fact]
	public void GetProvider_NullId_ShouldThrowArgumentNullException_Test() =>
		GetProvider_NullId_ShouldThrowArgumentNullException();

	#endregion GetProvider Method Tests

	#region GetPrimary Method Tests

	[Fact]
	public void GetPrimary_NoPrimary_ShouldThrowInvalidOperationException_Test() =>
		GetPrimary_NoPrimary_ShouldThrowInvalidOperationException();

	[Fact]
	public void GetPrimary_WithPrimary_ShouldReturnProvider_Test() =>
		GetPrimary_WithPrimary_ShouldReturnProvider();

	#endregion GetPrimary Method Tests

	#region SetPrimary Method Tests

	[Fact]
	public void SetPrimary_NullProviderId_ShouldThrowArgumentNullException_Test() =>
		SetPrimary_NullProviderId_ShouldThrowArgumentNullException();

	[Fact]
	public void SetPrimary_Unregistered_ShouldThrowInvalidOperationException_Test() =>
		SetPrimary_Unregistered_ShouldThrowInvalidOperationException();

	#endregion SetPrimary Method Tests

	#region GetLegacyProviders Method Tests

	[Fact]
	public void GetLegacyProviders_Initially_ShouldBeEmpty_Test() =>
		GetLegacyProviders_Initially_ShouldBeEmpty();

	#endregion GetLegacyProviders Method Tests

	#region GetAll Method Tests

	[Fact]
	public void GetAll_WithProviders_ShouldReturnAll_Test() =>
		GetAll_WithProviders_ShouldReturnAll();

	#endregion GetAll Method Tests

	#region FindDecryptionProvider Method Tests

	[Fact]
	public void FindDecryptionProvider_NullEncryptedData_ShouldThrowArgumentNullException_Test() =>
		FindDecryptionProvider_NullEncryptedData_ShouldThrowArgumentNullException();

	#endregion FindDecryptionProvider Method Tests

	/// <summary>
	/// Minimal stub implementation of IEncryptionProvider for registry testing.
	/// </summary>
	private sealed class StubEncryptionProvider : IEncryptionProvider
	{
		public Task<EncryptedData> EncryptAsync(
			byte[] plaintext,
			EncryptionContext context,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("Stub provider does not support encryption.");

		public Task<byte[]> DecryptAsync(
			EncryptedData encryptedData,
			EncryptionContext context,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException("Stub provider does not support decryption.");

		public Task<bool> ValidateFipsComplianceAsync(CancellationToken cancellationToken) =>
			Task.FromResult(false);
	}
}
