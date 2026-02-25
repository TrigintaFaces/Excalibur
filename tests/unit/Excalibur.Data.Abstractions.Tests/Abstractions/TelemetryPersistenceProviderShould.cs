// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class TelemetryPersistenceProviderShould
{
	[Fact]
	public void ThrowWhenInnerProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new TelemetryPersistenceProvider(null!));
	}

	[Fact]
	public async Task DelegateExecuteAsyncToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.Returns(Task.FromResult("result"));
		A.CallTo(() => inner.Name).Returns("TestProvider");
		A.CallTo(() => inner.ProviderType).Returns("SqlServer");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act
		var result = await sut.ExecuteAsync(request, CancellationToken.None);

		// Assert
		result.ShouldBe("result");
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateExceptionFromExecuteAsync()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var request = A.Fake<IDataRequest<IDisposable, string>>();
		A.CallTo(() => inner.ExecuteAsync(request, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("test error"));
		A.CallTo(() => inner.Name).Returns("TestProvider");
		A.CallTo(() => inner.ProviderType).Returns("SqlServer");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.ExecuteAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task DelegateInitializeAsyncToInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var options = A.Fake<IPersistenceOptions>();
		A.CallTo(() => inner.Name).Returns("TestProvider");
		A.CallTo(() => inner.ProviderType).Returns("SqlServer");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act
		await sut.InitializeAsync(options, CancellationToken.None);

		// Assert
		A.CallTo(() => inner.InitializeAsync(options, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateExceptionFromInitializeAsync()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		var options = A.Fake<IPersistenceOptions>();
		A.CallTo(() => inner.InitializeAsync(options, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("init error"));
		A.CallTo(() => inner.Name).Returns("TestProvider");
		A.CallTo(() => inner.ProviderType).Returns("SqlServer");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.InitializeAsync(options, CancellationToken.None));
	}

	[Fact]
	public void DelegateNameFromInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("MyProvider");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act & Assert
		sut.Name.ShouldBe("MyProvider");
	}

	[Fact]
	public void DelegateProviderTypeFromInnerProvider()
	{
		// Arrange
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.ProviderType).Returns("Postgres");
		var sut = new TelemetryPersistenceProvider(inner);

		// Act & Assert
		sut.ProviderType.ShouldBe("Postgres");
	}
}
