// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class IndexInitializerShould
{
	[Fact]
	public void ThrowWhenInitializersIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new IndexInitializer(null!));
	}

	[Fact]
	public async Task InitializeAllRegisteredInitializers()
	{
		// Arrange
		var initializer1 = A.Fake<IInitializeElasticIndex>();
		var initializer2 = A.Fake<IInitializeElasticIndex>();
		var initializer3 = A.Fake<IInitializeElasticIndex>();

		var sut = new IndexInitializer([initializer1, initializer2, initializer3]);

		// Act
		await sut.InitializeIndexesAsync();

		// Assert
		A.CallTo(() => initializer1.InitializeAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => initializer2.InitializeAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => initializer3.InitializeAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleEmptyInitializersList()
	{
		// Arrange
		var sut = new IndexInitializer(Enumerable.Empty<IInitializeElasticIndex>());

		// Act & Assert — should not throw
		await sut.InitializeIndexesAsync();
	}

	[Fact]
	public async Task InitializeInSequentialOrder()
	{
		// Arrange
		var callOrder = new List<int>();
		var initializer1 = A.Fake<IInitializeElasticIndex>();
		var initializer2 = A.Fake<IInitializeElasticIndex>();

		A.CallTo(() => initializer1.InitializeAsync(A<CancellationToken>._))
			.Invokes(() => callOrder.Add(1))
			.Returns(Task.CompletedTask);

		A.CallTo(() => initializer2.InitializeAsync(A<CancellationToken>._))
			.Invokes(() => callOrder.Add(2))
			.Returns(Task.CompletedTask);

		var sut = new IndexInitializer([initializer1, initializer2]);

		// Act
		await sut.InitializeIndexesAsync();

		// Assert
		callOrder.ShouldBe([1, 2]);
	}
}
