// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.KeyManagementService;

using Excalibur.Compliance;
using Excalibur.Compliance.Aws;

using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.KeyManagement;

/// <summary>
/// Conformance tests binding <see cref="KeyManagementProviderConformanceTestKit"/> to the REAL
/// <see cref="AwsKmsProvider"/> against a real LocalStack KMS service (see
/// <see cref="LocalStackContainerFixture"/>) — not a mock.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>.claude/rules/process/verify-against-real-infra-not-mock.md</c>, a mocked
/// <see cref="IAmazonKeyManagementService"/> can certify behavior a real KMS service rejects or simply
/// does not implement the way the framework's <see cref="IKeyManagementProvider"/> contract assumes.
/// This deriver runs every arm against the real LocalStack KMS API surface, deliberately WITHOUT
/// weakening any assertion — a RED arm here is evidence about <see cref="AwsKmsProvider"/>, not a defect
/// in the test.
/// </para>
/// <para>
/// <b>Isolation without a fresh server per test:</b> <see cref="AwsKmsProvider.ListKeysAsync"/> filters
/// KMS aliases by <see cref="AwsKmsOptions.KeyAliasPrefix"/>, so <see cref="CreateProvider"/> mints a
/// brand-new random alias prefix on every call, scoping each arm's provider to its own alias namespace on
/// the shared LocalStack container.
/// </para>
/// </remarks>
[Collection(LocalStackTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "PROVIDER")]
public sealed class AwsKmsKeyManagementProviderConformanceTests : KeyManagementProviderConformanceTestKit, IDisposable
{
	private readonly LocalStackContainerFixture _fixture;
	private readonly List<IMemoryCache> _caches = [];
	private IAmazonKeyManagementService? _kmsClient;

	public AwsKmsKeyManagementProviderConformanceTests(LocalStackContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IKeyManagementProvider CreateProvider()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"KeyManagementProviderConformanceTestKit arms against AwsKmsProvider must run against a real "
			+ "KMS API (LocalStack) -- never skipped. "
			+ (_fixture.InitializationError ?? "LocalStack container required."));

		// The KMS client is a stateless-per-call HTTP wrapper -- one instance is reused across every
		// CreateProvider() call in this test class; AwsKmsProvider never disposes an injected client.
		_kmsClient ??= _fixture.CreateKmsClient();

		var cache = new MemoryCache(new MemoryCacheOptions());
		_caches.Add(cache);

		var options = new AwsKmsOptions
		{
			// A fresh random alias prefix per provider instance scopes ListKeysAsync to aliases THIS
			// instance created, isolating each arm from the others on the shared LocalStack container.
			KeyAliasPrefix = $"conformance-{Guid.NewGuid():N}",
			KeyPolicy = new AwsKmsKeyPolicyOptions
			{
				// Immediate-rotation path only exercises CreateKey/EnableKeyRotation is a separate,
				// annual-schedule AWS feature this kit does not exercise; keep it off to avoid an
				// unrelated LocalStack KMS call on every RotateKeyAsync.
				EnableAutoRotation = false,
			},
		};

		return new AwsKmsProvider(
			_kmsClient,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<AwsKmsProvider>.Instance,
			cache);
	}

	public void Dispose()
	{
		foreach (var cache in _caches)
		{
			cache.Dispose();
		}
	}

	#region Suite wiring guard

	[Fact]
	public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring guard

	#region GetKey Tests

	[Fact]
	public Task GetKeyAsync_NonExistent_ShouldReturnNull_Test() =>
		GetKeyAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() =>
		GetKeyAsync_NullKeyId_ShouldThrowArgumentException();

	[Fact]
	public Task GetKeyAsync_ExistingKey_ShouldReturnLatestVersion_Test() =>
		GetKeyAsync_ExistingKey_ShouldReturnLatestVersion();

	#endregion GetKey Tests

	#region GetKeyVersion Tests

	[Fact]
	public Task GetKeyVersionAsync_NonExistentVersion_ShouldReturnNull_Test() =>
		GetKeyVersionAsync_NonExistentVersion_ShouldReturnNull();

	[Fact]
	public Task GetKeyVersionAsync_ExistingVersion_ShouldReturnCorrectMetadata_Test() =>
		GetKeyVersionAsync_ExistingVersion_ShouldReturnCorrectMetadata();

	#endregion GetKeyVersion Tests

	#region ListKeys Tests

	[Fact]
	public Task ListKeysAsync_NoKeys_ShouldReturnEmptyList_Test() =>
		ListKeysAsync_NoKeys_ShouldReturnEmptyList();

	[Fact]
	public Task ListKeysAsync_FilterByStatus_ShouldFilterCorrectly_Test() =>
		ListKeysAsync_FilterByStatus_ShouldFilterCorrectly();

	[Fact]
	public Task ListKeysAsync_FilterByPurpose_ShouldFilterCorrectly_Test() =>
		ListKeysAsync_FilterByPurpose_ShouldFilterCorrectly();

	#endregion ListKeys Tests

	#region RotateKey Tests

	[Fact]
	public Task RotateKeyAsync_NewKey_ShouldCreateVersion1_Test() =>
		RotateKeyAsync_NewKey_ShouldCreateVersion1();

	[Fact]
	public Task RotateKeyAsync_ExistingKey_ShouldCreateNewVersionAndMarkOldDecryptOnly_Test() =>
		RotateKeyAsync_ExistingKey_ShouldCreateNewVersionAndMarkOldDecryptOnly();

	[Fact]
	public Task RotateKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() =>
		RotateKeyAsync_NullKeyId_ShouldThrowArgumentException();

	#endregion RotateKey Tests

	#region DeleteKey Tests

	[Fact]
	public Task DeleteKeyAsync_NonExistent_ShouldReturnFalse_Test() =>
		DeleteKeyAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task DeleteKeyAsync_ExistingKey_ShouldScheduleForDestruction_Test() =>
		DeleteKeyAsync_ExistingKey_ShouldScheduleForDestruction();

	#endregion DeleteKey Tests

	#region SuspendKey Tests

	[Fact]
	public Task SuspendKeyAsync_NonExistent_ShouldReturnFalse_Test() =>
		SuspendKeyAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task SuspendKeyAsync_ExistingKey_ShouldSuspendAllVersions_Test() =>
		SuspendKeyAsync_ExistingKey_ShouldSuspendAllVersions();

	[Fact]
	public Task SuspendKeyAsync_NullReason_ShouldThrowArgumentException_Test() =>
		SuspendKeyAsync_NullReason_ShouldThrowArgumentException();

	#endregion SuspendKey Tests

	#region ReactivateKey Tests

	[Fact]
	public Task ReactivateKeyAsync_NonExistent_ShouldReturnFalse_Test() =>
		ReactivateKeyAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task ReactivateKeyAsync_SuspendedKey_ShouldRestoreToActive_Test() =>
		ReactivateKeyAsync_SuspendedKey_ShouldRestoreToActive();

	#endregion ReactivateKey Tests

	#region GetActiveKey Tests

	[Fact]
	public Task GetActiveKeyAsync_NoActiveKey_ShouldReturnNull_Test() =>
		GetActiveKeyAsync_NoActiveKey_ShouldReturnNull();

	[Fact]
	public Task GetActiveKeyAsync_ActiveKeyExists_ShouldReturnActiveKey_Test() =>
		GetActiveKeyAsync_ActiveKeyExists_ShouldReturnActiveKey();

	[Fact]
	public Task GetActiveKeyAsync_FilterByPurpose_ShouldFilterCorrectly_Test() =>
		GetActiveKeyAsync_FilterByPurpose_ShouldFilterCorrectly();

	[Fact]
	public Task GetActiveKeyAsync_SuspendedKey_ShouldNotReturn_Test() =>
		GetActiveKeyAsync_SuspendedKey_ShouldNotReturn();

	#endregion GetActiveKey Tests

	#region Disposable Tests

	[Fact]
	public Task Disposed_Provider_ShouldThrowObjectDisposedException_Test() =>
		Disposed_Provider_ShouldThrowObjectDisposedException();

	#endregion Disposable Tests
}
