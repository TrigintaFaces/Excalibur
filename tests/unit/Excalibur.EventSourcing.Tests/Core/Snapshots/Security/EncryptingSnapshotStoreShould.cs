// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots.Security;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots.Security;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EncryptingSnapshotStoreShould
{
	private readonly ISnapshotStore _innerStore;
	private readonly ISnapshotEncryptor _encryptor;
	private readonly EncryptingSnapshotStore _sut;

	public EncryptingSnapshotStoreShould()
	{
		_innerStore = A.Fake<ISnapshotStore>();
		_encryptor = A.Fake<ISnapshotEncryptor>();
		_sut = new EncryptingSnapshotStore(_innerStore, _encryptor);
	}

	[Fact]
	public async Task SaveSnapshotAsync_EncryptDataBeforeSaving()
	{
		// Arrange
		var originalData = new byte[] { 1, 2, 3 };
		var encryptedData = new byte[] { 10, 20, 30 };
		var snapshot = CreateSnapshot("agg-1", originalData);

#pragma warning disable CA2012
		A.CallTo(() => _encryptor.EncryptAsync(originalData, A<CancellationToken>._))
			.Returns(new ValueTask<byte[]>(encryptedData));

		ISnapshot? capturedSnapshot = null;
		A.CallTo(() => _innerStore.SaveSnapshotAsync(A<ISnapshot>._, A<CancellationToken>._))
			.Invokes((ISnapshot s, CancellationToken _) => capturedSnapshot = s)
			.Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		capturedSnapshot.ShouldNotBeNull();
		capturedSnapshot.Data.ShouldBe(encryptedData);
		capturedSnapshot.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_DecryptDataAfterLoading()
	{
		// Arrange
		var encryptedData = new byte[] { 10, 20, 30 };
		var decryptedData = new byte[] { 1, 2, 3 };
		var storedSnapshot = CreateSnapshot("agg-1", encryptedData);

#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(storedSnapshot));

		A.CallTo(() => _encryptor.DecryptAsync(encryptedData, A<CancellationToken>._))
			.Returns(new ValueTask<byte[]>(decryptedData));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Data.ShouldBe(decryptedData);
		result.AggregateId.ShouldBe("agg-1");
		result.Version.ShouldBe(1);
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnNull_WhenInnerReturnsNull()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		A.CallTo(() => _encryptor.DecryptAsync(A<byte[]>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task SaveSnapshotAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		Should.Throw<ArgumentNullException>(() => new EncryptingSnapshotStore(_innerStore, null!));
	}

	[Fact]
	public async Task PreserveMetadataAfterDecryption()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["key"] = "value" };
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("TestType");
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.Version).Returns(3);
		A.CallTo(() => snapshot.Data).Returns([1, 2]);
		A.CallTo(() => snapshot.Metadata).Returns(metadata);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);

#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));
		A.CallTo(() => _encryptor.DecryptAsync(A<byte[]>._, A<CancellationToken>._))
			.Returns(new ValueTask<byte[]>(new byte[] { 5, 6 }));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Metadata.ShouldBeSameAs(metadata);
		result.Version.ShouldBe(3);
		result.SnapshotId.ShouldBe("snap-1");
	}

	private static ISnapshot CreateSnapshot(string aggregateId, byte[] data)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns($"snap-{aggregateId}");
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns("TestType");
		A.CallTo(() => snapshot.Version).Returns(1);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => snapshot.Data).Returns(data);
		A.CallTo(() => snapshot.Metadata).Returns(null);
		return snapshot;
	}
}
