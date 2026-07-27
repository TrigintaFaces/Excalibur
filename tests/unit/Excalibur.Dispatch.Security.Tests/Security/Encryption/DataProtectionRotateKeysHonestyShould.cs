// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

using FakeItEasy;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Regression lock (bead 9so1s5) for <see cref="DataProtectionMessageEncryptionService.RotateKeysAsync"/>:
/// rotation MUST create real key material through the authoritative Data Protection key-ring
/// (<see cref="IKeyManager.CreateNewKey"/>), not fabricate a <see cref="System.Guid"/> and flip the id.
/// </summary>
/// <remarks>
/// <b>Defect (pre-fix):</b> <c>RotateKeysAsync</c> spun <c>Guid.NewGuid()</c> and set <c>CurrentKeyId</c> to it,
/// rotating <b>no actual key material</b> — a silent lie (the key-ring was never touched, so protectors kept
/// using the old key). <b>Fix:</b> delegate to <c>IKeyManager.CreateNewKey(activation, expiration)</c> and adopt
/// the key-ring's <c>KeyId</c>.
/// <para>
/// <b>Non-vacuity:</b> the lock asserts <c>CreateNewKey</c> is actually invoked AND that the surfaced/adopted key
/// id is the key-ring's id — both RED on the pre-fix fabrication (CreateNewKey never called; <c>CurrentKeyId</c>
/// would be a random Guid, never the key-ring's id).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait(TraitNames.Feature, TestFeatures.Encryption)]
public sealed class DataProtectionRotateKeysHonestyShould : IDisposable
{
	private readonly DataProtectionMessageEncryptionService _sut;
	private readonly IKeyManager _keyManager = A.Fake<IKeyManager>();
	private readonly EncryptionOptions _options = new() { IncludeMetadataHeader = false, KeyRotationIntervalDays = 30 };
	private readonly Guid _keyRingKeyId = Guid.Parse("11111111-2222-3333-4444-555555555555");

	public DataProtectionRotateKeysHonestyShould()
	{
		var key = A.Fake<IKey>();
		A.CallTo(() => key.KeyId).Returns(_keyRingKeyId);
		A.CallTo(() => _keyManager.CreateNewKey(A<DateTimeOffset>._, A<DateTimeOffset>._)).Returns(key);

		_sut = new DataProtectionMessageEncryptionService(
			A.Fake<IDataProtectionProvider>(),
			_keyManager,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<DataProtectionMessageEncryptionService>.Instance);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public async Task CreateRealKeyMaterialThroughTheKeyRing_NotFabricateAGuid()
	{
		var result = await _sut.RotateKeysAsync(CancellationToken.None);

		// The key-ring was actually asked to create new material (pre-fix: never called).
		A.CallTo(() => _keyManager.CreateNewKey(A<DateTimeOffset>._, A<DateTimeOffset>._))
			.MustHaveHappenedOnceExactly();

		// The adopted/surfaced key id is the key-ring's id, not a fabricated Guid.
		result.Success.ShouldBeTrue();
		_ = result.NewKey.ShouldNotBeNull();
		result.NewKey.KeyId.ShouldBe(_keyRingKeyId.ToString());
		_options.CurrentKeyId.ShouldBe(_keyRingKeyId.ToString());
	}
}
