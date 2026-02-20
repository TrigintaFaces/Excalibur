// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class DelegatingPersistenceProviderShould
{
	[Fact]
	public void ThrowWhenInnerProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new TestDelegatingProvider(null!));
	}

	[Fact]
	public void DelegateNameToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("TestProvider");
		var sut = new TestDelegatingProvider(inner);

		// Act & Assert
		sut.Name.ShouldBe("TestProvider");
	}

	[Fact]
	public void DelegateProviderTypeToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.ProviderType).Returns("SqlServer");
		var sut = new TestDelegatingProvider(inner);

		// Act & Assert
		sut.ProviderType.ShouldBe("SqlServer");
	}

	[Fact]
	public async Task DelegateExecuteAsyncToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Returns(Task.FromResult("result"));
		var sut = new TestDelegatingProvider(inner);

		// Act
		var result = await sut.ExecuteAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBe("result");
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateInitializeAsyncToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var options = A.Fake<IPersistenceOptions>();
		var sut = new TestDelegatingProvider(inner);

		// Act
		await sut.InitializeAsync(options, CancellationToken.None);

		// Assert
		A.CallTo(() => inner.InitializeAsync(options, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DelegateGetServiceToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var expectedService = new object();
		A.CallTo(() => inner.GetService(typeof(string))).Returns(expectedService);
		var sut = new TestDelegatingProvider(inner);

		// Act
		var result = sut.GetService(typeof(string));

		// Assert
		result.ShouldBeSameAs(expectedService);
	}

	[Fact]
	public void DelegateDisposeToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var sut = new TestDelegatingProvider(inner);

		// Act
		sut.Dispose();

		// Assert
		A.CallTo(() => inner.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DelegateDisposeAsyncToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
#pragma warning disable CA2012
		A.CallTo(() => inner.DisposeAsync()).Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012
		var sut = new TestDelegatingProvider(inner);

		// Act
		await sut.DisposeAsync();

		// Assert
		A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	private sealed class TestDelegatingProvider : DelegatingPersistenceProvider
	{
		public TestDelegatingProvider(IPersistenceProvider innerProvider) : base(innerProvider) { }
	}
}
