// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

namespace Excalibur.Dispatch.Middleware.Tests.Validation;

[Trait("Category", "Unit")]
public sealed class FluentValidationServiceCollectionExtensionsShould
{
	#region WithFluentValidation Tests

	[Fact]
	public void RegisterFluentValidatorResolverWhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithFluentValidation();
		});
		var provider = services.BuildServiceProvider();
		var resolver = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		_ = resolver.ShouldBeOfType<FluentValidatorResolver>();
	}

	[Fact]
	public void RegisterValidatorResolverAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithFluentValidation();
		});

		// Act
		var provider = services.BuildServiceProvider();
		var resolver1 = provider.GetRequiredService<IValidatorResolver>();
		var resolver2 = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		resolver1.ShouldBeSameAs(resolver2);
	}

	[Fact]
	public void ReturnDispatchBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;
		IDispatchBuilder? returnedBuilder = null;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			capturedBuilder = dispatch;
			returnedBuilder = dispatch.WithFluentValidation();
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
		_ = returnedBuilder.ShouldNotBeNull();
		returnedBuilder.ShouldBeSameAs(capturedBuilder);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.WithFluentValidation());
	}

	#endregion WithFluentValidation Tests

	#region WithAotFluentValidation Tests

	[Fact]
	public void RegisterAotFluentValidatorResolverWhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithAotFluentValidation();
		});
		var provider = services.BuildServiceProvider();
		var resolver = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		_ = resolver.ShouldBeOfType<AotFluentValidatorResolver>();
	}

	[Fact]
	public void RegisterAotValidatorResolverAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.WithAotFluentValidation();
		});

		// Act
		var provider = services.BuildServiceProvider();
		var resolver1 = provider.GetRequiredService<IValidatorResolver>();
		var resolver2 = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		resolver1.ShouldBeSameAs(resolver2);
	}

	[Fact]
	public void ReturnDispatchBuilderForChainingWithAot()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		IDispatchBuilder? capturedBuilder = null;
		IDispatchBuilder? returnedBuilder = null;

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			capturedBuilder = dispatch;
			returnedBuilder = dispatch.WithAotFluentValidation();
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
		_ = returnedBuilder.ShouldNotBeNull();
		returnedBuilder.ShouldBeSameAs(capturedBuilder);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenBuilderIsNullForAot()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.WithAotFluentValidation());
	}

	#endregion WithAotFluentValidation Tests
}
