// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExceptionMappingBuilderShould
{
	[Fact]
	public void Build_WithNoMappings_UseDefaultMapper()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(_ => { });

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var result = mapper.Map(new InvalidOperationException("test error"));

		// Assert
		result.ShouldNotBeNull();
		result.Title.ShouldBe("Internal Server Error");
	}

	[Fact]
	public void Build_WithCustomMapping_MapSpecificException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.Map<ArgumentException>(ex => new MessageProblemDetails
			{
				Type = "urn:test:argument-error",
				Title = "Argument Error",
				ErrorCode = 400,
				Detail = ex.Message,
			});
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var result = mapper.Map(new ArgumentException("bad arg"));

		// Assert
		result.ShouldNotBeNull();
		result.ErrorCode.ShouldBe(400);
		result.Title.ShouldBe("Argument Error");
		result.Detail.ShouldBe("bad arg");
	}

	[Fact]
	public void Build_WithMultipleMappings_UseFirstMatch()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.Map<ArgumentException>(_ => new MessageProblemDetails
			{
				Title = "First",
				ErrorCode = 400,
			});
			builder.Map<ArgumentException>(_ => new MessageProblemDetails
			{
				Title = "Second",
				ErrorCode = 422,
			});
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var result = mapper.Map(new ArgumentException("test"));

		// Assert
		result.Title.ShouldBe("First");
	}

	[Fact]
	public async Task Build_WithAsyncMapping_MapAsynchronously()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.MapAsync<InvalidOperationException>(async (ex, ct) =>
			{
				await Task.Delay(1, ct).ConfigureAwait(false);
				return new MessageProblemDetails
				{
					Title = "Async Mapped",
					ErrorCode = 503,
					Detail = ex.Message,
				};
			});
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var result = await mapper.MapAsync(
			new InvalidOperationException("async test"),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Title.ShouldBe("Async Mapped");
		result.ErrorCode.ShouldBe(503);
	}

	[Fact]
	public void Build_WithConditionalMapping_OnlyMapWhenPredicateMatches()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.MapWhen<ArgumentException>(
				ex => ex.ParamName == "special",
				_ => new MessageProblemDetails
				{
					Title = "Special Argument",
					ErrorCode = 400,
				});
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act - matching predicate
		var matchResult = mapper.Map(new ArgumentException("msg", "special"));

		// Assert
		matchResult.Title.ShouldBe("Special Argument");

		// Act - non-matching predicate (falls through to default)
		var noMatchResult = mapper.Map(new ArgumentException("msg", "other"));
		noMatchResult.Title.ShouldBe("Internal Server Error");
	}

	[Fact]
	public void Build_WithCustomDefaultMapper_UseCustomDefault()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.MapDefault(_ => new MessageProblemDetails
			{
				Title = "Custom Default",
				ErrorCode = 500,
			});
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var result = mapper.Map(new InvalidOperationException("test"));

		// Assert
		result.Title.ShouldBe("Custom Default");
	}

	[Fact]
	public void Build_WithApiExceptionMapping_MapApiException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(builder =>
		{
			builder.UseApiExceptionMapping();
		});

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act
		var apiException = new ApiException(404, "not found", null);
		var result = mapper.Map(apiException);

		// Assert
		result.ShouldNotBeNull();
		result.ErrorCode.ShouldBe(404);
	}

	[Fact]
	public void CanMap_ReturnTrueForAnyException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(_ => { });

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act & Assert - default mapper always handles
		mapper.CanMap(new InvalidOperationException("test")).ShouldBeTrue();
		mapper.CanMap(new ArgumentException("test")).ShouldBeTrue();
		mapper.CanMap(new NotSupportedException("test")).ShouldBeTrue();
	}

	[Fact]
	public void Map_ThrowOnNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(_ => { });

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => mapper.Map(null!));
	}

	[Fact]
	public void CanMap_ThrowOnNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(_ => { });

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => mapper.CanMap(null!));
	}

	[Fact]
	public async Task MapAsync_ThrowOnNullException()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExceptionMapping(_ => { });

		using var provider = services.BuildServiceProvider();
		var mapper = provider.GetRequiredService<IExceptionMapper>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => mapper.MapAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}
}
