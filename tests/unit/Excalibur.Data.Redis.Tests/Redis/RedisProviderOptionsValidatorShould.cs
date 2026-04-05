// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Redis;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Redis.Tests.Redis;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class RedisProviderOptionsValidatorShould
{
	// RedisProviderOptionsValidator is internal with no InternalsVisibleTo for this test project.
	// We instantiate via reflection.
	private static readonly Type ValidatorType = typeof(RedisProviderOptions).Assembly
		.GetType("Microsoft.Extensions.DependencyInjection.RedisProviderOptionsValidator")!;

	private readonly IValidateOptions<RedisProviderOptions> _validator;

	public RedisProviderOptionsValidatorShould()
	{
		ValidatorType.ShouldNotBeNull("RedisProviderOptionsValidator type not found in assembly");
		_validator = (IValidateOptions<RedisProviderOptions>)Activator.CreateInstance(ValidatorType)!;
	}

	private static RedisProviderOptions CreateValidOptions() => new()
	{
		ConnectionString = "localhost:6379",
	};

	[Fact]
	public void SucceedForValidOptions()
	{
		var options = CreateValidOptions();

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenConnectionStringIsEmpty(string connectionString)
	{
		var options = new RedisProviderOptions { ConnectionString = connectionString };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisProviderOptions.ConnectionString));
	}

	[Fact]
	public void FailWhenDatabaseIdIsNegative()
	{
		var options = CreateValidOptions();
		options.DatabaseId = -1;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisProviderOptions.DatabaseId));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenConnectTimeoutIsLessThanOne(int value)
	{
		var options = CreateValidOptions();
		options.Pool.ConnectTimeout = value;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisConnectionPoolOptions.ConnectTimeout));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenSyncTimeoutIsLessThanOne(int value)
	{
		var options = CreateValidOptions();
		options.Pool.SyncTimeout = value;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisConnectionPoolOptions.SyncTimeout));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenAsyncTimeoutIsLessThanOne(int value)
	{
		var options = CreateValidOptions();
		options.Pool.AsyncTimeout = value;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisConnectionPoolOptions.AsyncTimeout));
	}

	[Fact]
	public void FailWhenConnectRetryIsNegative()
	{
		var options = CreateValidOptions();
		options.Pool.ConnectRetry = -1;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisConnectionPoolOptions.ConnectRetry));
	}

	[Fact]
	public void SucceedWhenConnectRetryIsZero()
	{
		var options = CreateValidOptions();
		options.Pool.ConnectRetry = 0;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		var options = new RedisProviderOptions
		{
			ConnectionString = "",
			DatabaseId = -1,
		};
		options.Pool.ConnectTimeout = 0;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RedisProviderOptions.ConnectionString));
		result.FailureMessage.ShouldContain(nameof(RedisProviderOptions.DatabaseId));
		result.FailureMessage.ShouldContain(nameof(RedisConnectionPoolOptions.ConnectTimeout));
	}
}
