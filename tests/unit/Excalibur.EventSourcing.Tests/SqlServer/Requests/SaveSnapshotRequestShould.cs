// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.SqlServer.Requests;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SaveSnapshotRequestShould
{
	[Fact]
	public void CreateSuccessfully()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("Order");
		A.CallTo(() => snapshot.Version).Returns(5L);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);

		// Act
		var sut = new SaveSnapshotRequest(snapshot, CancellationToken.None);

		// Assert
		sut.ShouldNotBeNull();
		sut.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ThrowWhenSnapshotIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SaveSnapshotRequest(null!, CancellationToken.None));
	}

	[Fact]
	public void ExposeResolveAsync()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.Data).Returns([]);

		// Act
		var sut = new SaveSnapshotRequest(snapshot, CancellationToken.None);

		// Assert
		sut.ResolveAsync.ShouldNotBeNull();
	}
}
