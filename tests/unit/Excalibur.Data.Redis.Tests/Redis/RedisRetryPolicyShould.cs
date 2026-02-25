// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Excalibur.Data.Redis;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for RedisRetryPolicy.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisRetryPolicyShould : UnitTestBase
{
	private static object CreateRedisRetryPolicy(int maxRetryAttempts)
	{
		// RedisRetryPolicy is internal, so we use reflection to create and test it
		var assembly = typeof(RedisProviderOptions).Assembly;
		var retryPolicyType = assembly.GetType("Excalibur.Data.Redis.RedisRetryPolicy");
		if (retryPolicyType == null)
		{
			throw new InvalidOperationException("Could not find RedisRetryPolicy type");
		}

		var logger = NullLogger.Instance;
		var instance = Activator.CreateInstance(retryPolicyType, maxRetryAttempts, logger);
		if (instance == null)
		{
			throw new InvalidOperationException("Could not create RedisRetryPolicy instance");
		}

		return instance;
	}

	private static T InvokeMethod<T>(object instance, string methodName, params object[] parameters)
	{
		// Find method by matching parameter count and types to handle overloads
		var paramTypes = parameters.Select(p => p.GetType()).ToArray();
		var method = instance.GetType().GetMethods()
			.Where(m => m.Name == methodName && m.GetParameters().Length == parameters.Length)
			.FirstOrDefault(m =>
			{
				var methodParams = m.GetParameters();
				for (var i = 0; i < methodParams.Length; i++)
				{
					if (!methodParams[i].ParameterType.IsAssignableFrom(paramTypes[i]))
					{
						return false;
					}
				}
				return true;
			});

		if (method == null)
		{
			throw new InvalidOperationException($"Could not find method {methodName} with {parameters.Length} parameters");
		}

		var result = method.Invoke(instance, parameters);
		return (T)result!;
	}

	private static T GetProperty<T>(object instance, string propertyName)
	{
		var property = instance.GetType().GetProperty(propertyName);
		if (property == null)
		{
			throw new InvalidOperationException($"Could not find property {propertyName}");
		}

		return (T)property.GetValue(instance)!;
	}

	#region Static Properties

	[Fact]
	public void HaveCorrectInitialDelay()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var type = policy.GetType();

		// Act
		var initialDelayProperty = type.GetProperty("InitialDelay", BindingFlags.Public | BindingFlags.Static);
		var initialDelay = (TimeSpan)initialDelayProperty!.GetValue(null)!;

		// Assert
		initialDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveCorrectMaxDelay()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var type = policy.GetType();

		// Act
		var maxDelayProperty = type.GetProperty("MaxDelay", BindingFlags.Public | BindingFlags.Static);
		var maxDelay = (TimeSpan)maxDelayProperty!.GetValue(null)!;

		// Assert
		maxDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion Static Properties

	#region Instance Properties

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	public void ReturnConfiguredMaxRetryAttempts(int maxRetryAttempts)
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(maxRetryAttempts);

		// Act
		var result = GetProperty<int>(policy, "MaxRetryAttempts");

		// Assert
		result.ShouldBe(maxRetryAttempts);
	}

	[Fact]
	public void HaveCorrectBaseRetryDelay()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);

		// Act
		var result = GetProperty<TimeSpan>(policy, "BaseRetryDelay");

		// Assert
		result.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion Instance Properties

	#region ShouldRetry with Exception Only

	[Fact]
	public void ReturnTrue_ForRedisException()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var exception = new RedisException("Test exception");

		// Act
		var result = InvokeMethod<bool>(policy, "ShouldRetry", exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_ForRedisTimeoutException()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var exception = new RedisTimeoutException("Timeout", CommandStatus.Unknown);

		// Act
		var result = InvokeMethod<bool>(policy, "ShouldRetry", exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_ForRedisConnectionException()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var exception = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");

		// Act
		var result = InvokeMethod<bool>(policy, "ShouldRetry", exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_ForNonRedisException()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var exception = new InvalidOperationException("Not a Redis exception");

		// Act
		var result = InvokeMethod<bool>(policy, "ShouldRetry", exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_ForArgumentException()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(3);
		var exception = new ArgumentException("Invalid argument");

		// Act
		var result = InvokeMethod<bool>(policy, "ShouldRetry", exception);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion ShouldRetry with Exception Only

	#region ShouldRetry with AttemptNumber

	[Theory]
	[InlineData(0, 3, true)]  // First attempt
	[InlineData(1, 3, true)]  // Second attempt
	[InlineData(2, 3, true)]  // Third attempt
	[InlineData(3, 3, true)]  // Fourth attempt (at limit)
	[InlineData(4, 3, false)] // Fifth attempt (over limit)
	public void RespectMaxRetryAttempts_ForRedisException(int attemptNumber, int maxRetry, bool expectedResult)
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(maxRetry);
		var exception = new RedisException("Test");

		// Find the ShouldRetry method with two parameters
		var method = policy.GetType().GetMethods()
			.First(m => m.Name == "ShouldRetry" && m.GetParameters().Length == 2);

		// Act
		var result = (bool)method.Invoke(policy, new object[] { exception, attemptNumber })!;

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Theory]
	[InlineData(0, 3, false)] // Even first attempt should fail for non-retryable
	[InlineData(1, 3, false)]
	public void ReturnFalse_ForNonRetryableException_RegardlessOfAttempt(int attemptNumber, int maxRetry, bool expectedResult)
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(maxRetry);
		var exception = new InvalidOperationException("Not retryable");

		// Find the ShouldRetry method with two parameters
		var method = policy.GetType().GetMethods()
			.First(m => m.Name == "ShouldRetry" && m.GetParameters().Length == 2);

		// Act
		var result = (bool)method.Invoke(policy, new object[] { exception, attemptNumber })!;

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion ShouldRetry with AttemptNumber

	#region GetDelay

	[Theory]
	[InlineData(1, 2)]   // 2^1 = 2 seconds
	[InlineData(2, 4)]   // 2^2 = 4 seconds
	[InlineData(3, 8)]   // 2^3 = 8 seconds
	[InlineData(4, 16)]  // 2^4 = 16 seconds
	[InlineData(5, 30)]  // 2^5 = 32 seconds, but capped at 30
	[InlineData(6, 30)]  // 2^6 = 64 seconds, but capped at 30
	public void CalculateExponentialBackoff_WithMaxCap(int attemptNumber, double expectedSeconds)
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(10);

		// Act
		var delay = InvokeMethod<TimeSpan>(policy, "GetDelay", attemptNumber);

		// Assert
		delay.TotalSeconds.ShouldBe(expectedSeconds);
	}

	[Fact]
	public void NotExceedMaxDelay()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(10);

		// Act - try high attempt number (but not so high it overflows)
		var delay = InvokeMethod<TimeSpan>(policy, "GetDelay", 10);

		// Assert - delay should be capped at max 30 seconds
		delay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion GetDelay

	#region Zero MaxRetryAttempts

	[Fact]
	public void AllowZeroMaxRetryAttempts()
	{
		// Arrange & Act
		var policy = CreateRedisRetryPolicy(0);

		// Assert
		GetProperty<int>(policy, "MaxRetryAttempts").ShouldBe(0);
	}

	[Fact]
	public void ReturnTrue_OnFirstAttempt_WithZeroMaxRetry()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(0);
		var exception = new RedisException("Test");

		// Find the ShouldRetry method with two parameters
		var method = policy.GetType().GetMethods()
			.First(m => m.Name == "ShouldRetry" && m.GetParameters().Length == 2);

		// Act
		var result = (bool)method.Invoke(policy, new object[] { exception, 0 })!;

		// Assert - attempt 0 <= maxRetry 0, so true
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_OnSecondAttempt_WithZeroMaxRetry()
	{
		// Arrange
		var policy = CreateRedisRetryPolicy(0);
		var exception = new RedisException("Test");

		// Find the ShouldRetry method with two parameters
		var method = policy.GetType().GetMethods()
			.First(m => m.Name == "ShouldRetry" && m.GetParameters().Length == 2);

		// Act
		var result = (bool)method.Invoke(policy, new object[] { exception, 1 })!;

		// Assert - attempt 1 > maxRetry 0, so false
		result.ShouldBeFalse();
	}

	#endregion Zero MaxRetryAttempts
}
