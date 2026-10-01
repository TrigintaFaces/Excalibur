// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.KeyRotation;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.KeyRotation;

/// <summary>
/// Where <see cref="KeyMetadata.CreatedAt"/> decides whether a key is old enough to rotate, an UNKNOWN
/// instant counts as STALE — due, and exceeding the maximum age — never as recent.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS DIRECTION AND NOT THE OTHER.</b> The field can now report <see langword="null"/> when the
/// backend supplies no instant, which is the honest alternative to stamping a clock. That honesty moves a
/// decision to every reader: what is the age of a key whose creation nobody knows? Nothing can show such a
/// key is still inside its maximum age, and the only safe reading of "cannot be shown to be young enough" is
/// "rotate it". The cost is a rotation that may not have been needed. The cost of the other direction is a
/// key that outlives its maximum age forever, silently, which is the property the setting exists to provide.
/// </para>
/// <para>
/// <b>THE LANGUAGE PICKS THE WRONG DIRECTION BY DEFAULT, WHICH IS WHY EVERY SITE IS EXPLICIT.</b> A lifted
/// comparison over a null <see cref="TimeSpan"/> yields <see langword="false"/>: both
/// <c>timeSinceRotation &gt;= MaxKeyAge</c> and <c>keyAge &gt; MaxKeyAge</c> report NOT due for an undatable
/// key if the absence is left to the compiler. So the dangerous behaviour here is not something a careless
/// edit would introduce — it is what you get by writing the obvious thing and not thinking about it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AnUnknownKeyAgeCountsAsStaleShould
{
	/// <summary>
	/// THE LOAD-BEARING ARM for the policy. A key with no known creation instant is due for rotation.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: let the lifted comparison decide the absence and this reports NOT due,
	/// so a key whose age cannot be established is never rotated and quietly outlives the maximum age.
	/// </remarks>
	[Fact]
	public void TreatAKeyWithNoKnownCreationInstantAsDueForRotation()
	{
		var policy = new KeyRotationPolicy
		{
			AutoRotateEnabled = true,
			MaxKeyAge = TimeSpan.FromDays(30),
		};

		policy.IsRotationDue(UndatedKey()).ShouldBeTrue(
			"nothing can show this key is within its maximum age, so it must be treated as due. Reporting "
			+ "NOT due here lets a key of unbounded age keep encrypting, which is the guarantee MaxKeyAge "
			+ "exists to give");
	}

	/// <summary>
	/// LIVENESS. Without it the arm above is satisfied by a policy that calls EVERY key due, which would
	/// rotate continuously and make the maximum age meaningless in the other direction.
	/// </summary>
	[Fact]
	public void StillLeaveAYoungDatedKeyAlone()
	{
		var policy = new KeyRotationPolicy
		{
			AutoRotateEnabled = true,
			MaxKeyAge = TimeSpan.FromDays(30),
		};

		policy.IsRotationDue(DatedKey(ageInDays: 1)).ShouldBeFalse(
			"a key created yesterday is inside a thirty-day maximum");
	}

	/// <summary>
	/// SAFETY twin for the same policy: a dated key that HAS exceeded the maximum is still due, so the
	/// absent-case branch did not displace the ordinary comparison.
	/// </summary>
	[Fact]
	public void StillRotateADatedKeyPastItsMaximumAge()
	{
		var policy = new KeyRotationPolicy
		{
			AutoRotateEnabled = true,
			MaxKeyAge = TimeSpan.FromDays(30),
		};

		policy.IsRotationDue(DatedKey(ageInDays: 60)).ShouldBeTrue();
	}

	/// <summary>
	/// The schedule agrees with the verdict: a key of unknown age is not given a future rotation time, which
	/// would read as "already handled, come back later".
	/// </summary>
	[Fact]
	public void NotScheduleAFutureRotationForAKeyOfUnknownAge()
	{
		var policy = new KeyRotationPolicy
		{
			AutoRotateEnabled = true,
			MaxKeyAge = TimeSpan.FromDays(30),
		};

		policy.GetNextRotationTime(UndatedKey()).ShouldBeLessThanOrEqualTo(
			DateTimeOffset.UtcNow,
			"a key that is already due cannot also be scheduled for the future. A plausible future instant "
			+ "here is the fabrication problem returning one layer out: the age was unknown and the schedule "
			+ "pretends otherwise");
	}

	/// <summary>
	/// THE LOAD-BEARING ARM for the encryption path, and the mutant named in the ruling: make a null resolve
	/// to <c>UtcNow</c> here and this arm must fail.
	/// </summary>
	/// <remarks>
	/// This is the site where a substituted clock did the most harm, because it is evaluated before every
	/// encryption: an undatable key would be re-read as created this instant, so it would never appear to
	/// exceed the maximum and would keep encrypting indefinitely.
	/// </remarks>
	[Fact]
	public async Task RotateBeforeEncryptingUnderAKeyOfUnknownAge()
	{
		var keyManagement = A.Fake<IKeyManagementProvider>();
		var inner = A.Fake<IEncryptionProvider>();

		_ = A.CallTo(() => keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(UndatedKey()));
		_ = A.CallTo(() => keyManagement.RotateKeyAsync(
				"k1", EncryptionAlgorithm.Aes256Gcm, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new KeyRotationResult
			{
				Success = true,
				NewKey = DatedKey(ageInDays: 0),
			}));
		_ = A.CallTo(() => inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 2,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		using var sut = new RotatingEncryptionProvider(
			inner,
			keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance,
			new RotatingEncryptionOptions
			{
				AutoRotateBeforeEncryption = true,
				MaxKeyAge = TimeSpan.FromDays(30),
			});

		_ = await sut.EncryptAsync([1, 2], new EncryptionContext(), CancellationToken.None);

		A.CallTo(() => keyManagement.RotateKeyAsync(
				"k1", EncryptionAlgorithm.Aes256Gcm, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// LIVENESS for the encryption path. Without it the arm above is satisfied by a provider that rotates
	/// before every single encryption, which would be ruinous and would also pass a rotation assertion.
	/// </summary>
	[Fact]
	public async Task NotRotateBeforeEncryptingUnderAYoungDatedKey()
	{
		var keyManagement = A.Fake<IKeyManagementProvider>();
		var inner = A.Fake<IEncryptionProvider>();

		_ = A.CallTo(() => keyManagement.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(DatedKey(ageInDays: 1)));
		_ = A.CallTo(() => inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1],
				Iv = new byte[12],
				KeyId = "k1",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		using var sut = new RotatingEncryptionProvider(
			inner,
			keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance,
			new RotatingEncryptionOptions
			{
				AutoRotateBeforeEncryption = true,
				MaxKeyAge = TimeSpan.FromDays(30),
			});

		_ = await sut.EncryptAsync([1, 2], new EncryptionContext(), CancellationToken.None);

		A.CallTo(() => keyManagement.RotateKeyAsync(
				A<string>._, A<EncryptionAlgorithm>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>A key the backend did not date — the state the whole fixture is about.</summary>
	private static KeyMetadata UndatedKey() => new()
	{
		KeyId = "k1",
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = null,
	};

	private static KeyMetadata DatedKey(int ageInDays) => new()
	{
		KeyId = "k1",
		Version = ageInDays == 0 ? 2 : 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow.AddDays(-ageInDays),
	};
}
