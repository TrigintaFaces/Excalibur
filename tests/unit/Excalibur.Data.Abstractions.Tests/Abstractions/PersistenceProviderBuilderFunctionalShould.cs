// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
public class PersistenceProviderBuilderFunctionalShould
{
	[Fact]
	public void Constructor_WithNull_ShouldThrow()
	{
		Should.Throw<ArgumentNullException>(() => new PersistenceProviderBuilder(null!));
	}

	[Fact]
	public void Build_WithNoDecorators_ShouldReturnInnerProvider()
	{
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("InnerProvider");

		var result = new PersistenceProviderBuilder(inner).Build();

		result.ShouldBeSameAs(inner);
	}

	[Fact]
	public void Build_WithSingleDecorator_ShouldWrapInnerProvider()
	{
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("InnerProvider");

		var result = new PersistenceProviderBuilder(inner)
			.Use(p => new LoggingPersistenceProvider(p))
			.Build();

		result.ShouldBeOfType<LoggingPersistenceProvider>();
		result.Name.ShouldBe("InnerProvider"); // delegates to inner
	}

	[Fact]
	public void Build_WithMultipleDecorators_ShouldApplyInRegistrationOrder()
	{
		var inner = A.Fake<IPersistenceProvider>();
		A.CallTo(() => inner.Name).Returns("Base");

		// First registered = outermost
		var result = new PersistenceProviderBuilder(inner)
			.Use(p => new CountingPersistenceProvider(p))
			.Use(p => new LoggingPersistenceProvider(p))
			.Build();

		// Outermost should be LoggingPersistenceProvider (last applied)
		result.ShouldBeOfType<LoggingPersistenceProvider>();
	}

	[Fact]
	public void Use_WithNull_ShouldThrow()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		Should.Throw<ArgumentNullException>(() => builder.Use(null!));
	}

	[Fact]
	public void Use_ShouldReturnBuilderForChaining()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var builder = new PersistenceProviderBuilder(inner);

		var result = builder.Use(p => new LoggingPersistenceProvider(p));

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public async Task Build_DecoratorChain_ShouldDelegateCallsCorrectly()
	{
		var inner = A.Fake<IPersistenceProvider>();
		var options = A.Fake<IPersistenceOptions>();

		var result = new PersistenceProviderBuilder(inner)
			.Use(p => new CountingPersistenceProvider(p))
			.Use(p => new LoggingPersistenceProvider(p))
			.Build();

		await result.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		// Inner provider should have been called (through decorator chain)
		A.CallTo(() => inner.InitializeAsync(options, CancellationToken.None))
			.MustHaveHappenedOnceExactly();

		// Check decorators worked
		var logging = (LoggingPersistenceProvider)result;
		logging.LogEntries.ShouldContain("Before Initialize");
		logging.LogEntries.ShouldContain("After Initialize");
	}
}
