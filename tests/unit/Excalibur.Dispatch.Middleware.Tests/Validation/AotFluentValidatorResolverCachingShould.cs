// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;

namespace Excalibur.Dispatch.Middleware.Tests.Validation;

/// <summary>
/// Tests for validator array caching behavior in <see cref="AotFluentValidatorResolver"/>.
/// Verifies that <c>GetOrCacheValidators&lt;TMessage&gt;()</c> resolves from DI on first call
/// and returns the cached array on subsequent calls.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AotFluentValidatorResolverCachingShould : UnitTestBase
{
	#region Test Messages

	private sealed record CacheTestMessage(string Name) : IDispatchMessage;

	private sealed record OtherCacheTestMessage(int Value) : IDispatchMessage;

	private sealed record EmptyCacheMessage(string Data) : IDispatchMessage;

	#endregion Test Messages

	#region Test Validators

	private sealed class CacheTestMessageValidator : AbstractValidator<CacheTestMessage>
	{
		public CacheTestMessageValidator()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required");
		}
	}

	private sealed class OtherCacheTestMessageValidator : AbstractValidator<OtherCacheTestMessage>
	{
		public OtherCacheTestMessageValidator()
		{
			_ = RuleFor(x => x.Value)
				.GreaterThan(0)
				.WithMessage("Value must be positive");
		}
	}

	#endregion Test Validators

	#region Helper Methods

	private static AotFluentValidatorResolver CreateResolver(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		configure?.Invoke(services);
		var provider = services.BuildServiceProvider();
		return new AotFluentValidatorResolver(provider);
	}

	private static ConcurrentDictionary<Type, object>? GetCacheField(AotFluentValidatorResolver resolver)
	{
		var field = typeof(AotFluentValidatorResolver)
			.GetField("_validatorCache", BindingFlags.NonPublic | BindingFlags.Instance);
		return field?.GetValue(resolver) as ConcurrentDictionary<Type, object>;
	}

	#endregion Helper Methods

	[Fact]
	public void ReturnNullWhenNoValidatorsRegistered()
	{
		// Arrange
		var sut = CreateResolver();
		var message = new EmptyCacheMessage("test");

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnSuccessWhenValidatorPasses()
	{
		// Arrange
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
		});
		var message = new CacheTestMessage("valid");

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailureWithErrorDetails()
	{
		// Arrange
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
		});
		var message = new CacheTestMessage("");

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ReturnConsistentResults_OnRepeatedCallsWithSameType()
	{
		// Arrange -- validates caching does not break correctness
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
		});
		var validMessage = new CacheTestMessage("valid");
		var invalidMessage = new CacheTestMessage("");

		// Act -- first call populates cache, second call uses cache
		var result1 = sut.ValidateTyped(validMessage);
		var result2 = sut.ValidateTyped(validMessage);
		var result3 = sut.ValidateTyped(invalidMessage);

		// Assert
		Assert.NotNull(result1);
		result1.IsValid.ShouldBeTrue();

		Assert.NotNull(result2);
		result2.IsValid.ShouldBeTrue();

		Assert.NotNull(result3);
		result3.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CacheValidatorArray_AfterFirstCall()
	{
		// Arrange
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
		});

		// Act -- first call should populate the cache
		_ = sut.ValidateTyped(new CacheTestMessage("test"));

		// Assert -- internal cache should contain the type key
		var cache = GetCacheField(sut);
		Assert.NotNull(cache);
		cache.ContainsKey(typeof(CacheTestMessage)).ShouldBeTrue();
	}

	[Fact]
	public void CacheEmptyValidatorArray_WhenNoValidatorsRegistered()
	{
		// Arrange
		var sut = CreateResolver();

		// Act -- call with no validators registered
		var result1 = sut.ValidateTyped(new EmptyCacheMessage("first"));
		var result2 = sut.ValidateTyped(new EmptyCacheMessage("second"));

		// Assert -- both should be null (empty array cached, length == 0)
		Assert.Null(result1);
		Assert.Null(result2);

		// Cache should contain the type key even for empty arrays
		var cache = GetCacheField(sut);
		Assert.NotNull(cache);
		cache.ContainsKey(typeof(EmptyCacheMessage)).ShouldBeTrue();
	}

	[Fact]
	public void CacheValidatorsIndependently_ForDifferentMessageTypes()
	{
		// Arrange
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
			_ = services.AddScoped<IValidator<OtherCacheTestMessage>, OtherCacheTestMessageValidator>();
		});

		// Act
		var result1 = sut.ValidateTyped(new CacheTestMessage("valid"));
		var result2 = sut.ValidateTyped(new OtherCacheTestMessage(42));

		// Assert -- both types independently validated
		Assert.NotNull(result1);
		result1.IsValid.ShouldBeTrue();

		Assert.NotNull(result2);
		result2.IsValid.ShouldBeTrue();

		// Cache should contain both types
		var cache = GetCacheField(sut);
		Assert.NotNull(cache);
		cache.Count.ShouldBe(2);
		cache.ContainsKey(typeof(CacheTestMessage)).ShouldBeTrue();
		cache.ContainsKey(typeof(OtherCacheTestMessage)).ShouldBeTrue();
	}

	[Fact]
	public void NotCrossContaminate_ValidationBetweenDifferentMessageTypes()
	{
		// Arrange -- register validator for one type but not the other
		var sut = CreateResolver(services =>
		{
			_ = services.AddScoped<IValidator<CacheTestMessage>, CacheTestMessageValidator>();
			// No validator registered for OtherCacheTestMessage
		});

		// Act
		var resultWithValidator = sut.ValidateTyped(new CacheTestMessage(""));
		var resultWithoutValidator = sut.ValidateTyped(new OtherCacheTestMessage(0));

		// Assert
		Assert.NotNull(resultWithValidator);
		resultWithValidator.IsValid.ShouldBeFalse(); // Fails validation

		Assert.Null(resultWithoutValidator); // No validators, returns null
	}
}
