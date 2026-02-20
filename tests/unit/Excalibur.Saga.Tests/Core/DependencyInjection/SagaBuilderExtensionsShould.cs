// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Core.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaBuilderExtensionsShould
{
	private sealed class TestSagaBuilder : ISagaBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithOrchestration()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithOrchestration());
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithOrchestrationWithAction()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithOrchestration((Action<AdvancedSagaOptions>)null!));
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithOrchestrationWithBuilderAction()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithOrchestration((Action<AdvancedSagaBuilder>)null!));
	}

	[Fact]
	public void ThrowWhenConfigureIsNullForWithOrchestrationWithBuilderAction()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithOrchestration((Action<AdvancedSagaBuilder>)null!));
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithCoordination()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithCoordination());
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithTimeouts()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithTimeouts());
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithInstrumentation()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithInstrumentation());
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForWithOutbox()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).WithOutbox());
	}

	[Fact]
	public void ReturnBuilderForChainingFromWithOrchestration()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithOrchestration();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnBuilderForChainingFromWithCoordination()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithCoordination();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnBuilderForChainingFromWithTimeouts()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithTimeouts();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnBuilderForChainingFromWithInstrumentation()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithInstrumentation();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ReturnBuilderForChainingFromWithOutbox()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithOutbox();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithTimeoutsAcceptsConfigureAction()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithTimeouts(opts =>
		{
			opts.PollInterval = TimeSpan.FromSeconds(30);
		});

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithTimeoutsAcceptsNullConfigureAction()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithTimeouts(null);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithOutboxAcceptsConfigureAction()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithOutbox(opts => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithOutboxAcceptsNullConfigureAction()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.WithOutbox(null);

		// Assert
		result.ShouldBeSameAs(builder);
	}
}
