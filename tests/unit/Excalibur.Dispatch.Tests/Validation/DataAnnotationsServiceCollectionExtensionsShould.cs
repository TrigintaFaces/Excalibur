// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.DataAnnotations;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
public sealed class DataAnnotationsServiceCollectionExtensionsShould
{
	#region Registration Tests

	[Fact]
	public void RegisterDataAnnotationsValidatorResolverWhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());
		var provider = services.BuildServiceProvider();
		var resolver = provider.GetRequiredService<IValidatorResolver>();

		// Assert
		_ = resolver.ShouldBeOfType<DataAnnotationsValidatorResolver>();
	}

	[Fact]
	public void ReplaceNoOpValidatorResolverWithDataAnnotationsResolver()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - AddDispatch registers NoOpValidatorResolver, WithDataAnnotationsValidation should replace it
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());
		var provider = services.BuildServiceProvider();
		var resolvers = provider.GetServices<IValidatorResolver>().ToList();

		// Assert - Should only have one resolver (DataAnnotations)
		resolvers.Count.ShouldBe(1);
		_ = resolvers.Single().ShouldBeOfType<DataAnnotationsValidatorResolver>();
	}

	[Fact]
	public void RegisterValidatorResolverAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => dispatch.WithDataAnnotationsValidation());

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

		// Act
		_ = services.AddDispatch(dispatch =>
		{
			var returnedBuilder = dispatch.WithDataAnnotationsValidation();
			capturedBuilder = dispatch;

			// Assert - method should return the same builder for chaining
			returnedBuilder.ShouldBeSameAs(dispatch);
		});

		// Assert
		_ = capturedBuilder.ShouldNotBeNull();
	}

	#endregion Registration Tests

	#region Exception Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenBuilderIsNull()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.WithDataAnnotationsValidation());
	}

	#endregion Exception Tests
}
