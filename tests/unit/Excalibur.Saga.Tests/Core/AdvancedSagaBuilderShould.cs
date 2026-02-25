// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Unit tests for <see cref="AdvancedSagaBuilder"/>.
/// Verifies fluent builder configuration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class AdvancedSagaBuilderShould
{
	private readonly IServiceCollection _services;
	private readonly AdvancedSagaOptions _options;
	private readonly AdvancedSagaBuilder _sut;

	public AdvancedSagaBuilderShould()
	{
		_services = new ServiceCollection();
		_options = new AdvancedSagaOptions();
		_sut = new AdvancedSagaBuilder(_services, _options);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaBuilder(null!, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AdvancedSagaBuilder(_services, null!));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		var builder = new AdvancedSagaBuilder(_services, _options);

		// Assert
		builder.ShouldNotBeNull();
		builder.Services.ShouldBe(_services);
	}

	#endregion

	#region Services Property Tests

	[Fact]
	public void ExposeServiceCollection()
	{
		// Assert
		_sut.Services.ShouldBe(_services);
	}

	#endregion

	#region UseRetryPolicy Tests

	[Fact]
	public void RegisterRetryPolicy_WhenCallingUseRetryPolicy()
	{
		// Act
		_sut.UseRetryPolicy<DummySagaRetryPolicy>();
		var provider = _services.BuildServiceProvider();

		// Assert
		var policy = provider.GetService<ISagaRetryPolicy>();
		policy.ShouldNotBeNull();
		policy.ShouldBeOfType<DummySagaRetryPolicy>();
	}

	[Fact]
	public void ReturnBuilderForChaining_UseRetryPolicy()
	{
		// Act
		var result = _sut.UseRetryPolicy<DummySagaRetryPolicy>();

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithDefaultTimeout Tests

	[Fact]
	public void SetDefaultTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromMinutes(45);

		// Act
		_sut.WithDefaultTimeout(timeout);

		// Assert
		_options.DefaultTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void ReturnBuilderForChaining_WithDefaultTimeout()
	{
		// Act
		var result = _sut.WithDefaultTimeout(TimeSpan.FromMinutes(1));

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithStepTimeout Tests

	[Fact]
	public void SetStepTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromSeconds(90);

		// Act
		_sut.WithStepTimeout(timeout);

		// Assert
		_options.DefaultStepTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void ReturnBuilderForChaining_WithStepTimeout()
	{
		// Act
		var result = _sut.WithStepTimeout(TimeSpan.FromSeconds(30));

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithMaxRetries Tests

	[Fact]
	public void SetMaxRetries()
	{
		// Act
		_sut.WithMaxRetries(10);

		// Assert
		_options.MaxRetryAttempts.ShouldBe(10);
	}

	[Fact]
	public void ReturnBuilderForChaining_WithMaxRetries()
	{
		// Act
		var result = _sut.WithMaxRetries(5);

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithMaxParallelism Tests

	[Fact]
	public void SetMaxParallelism()
	{
		// Act
		_sut.WithMaxParallelism(16);

		// Assert
		_options.MaxDegreeOfParallelism.ShouldBe(16);
	}

	[Fact]
	public void ReturnBuilderForChaining_WithMaxParallelism()
	{
		// Act
		var result = _sut.WithMaxParallelism(4);

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithAutoCompensation Tests

	[Fact]
	public void EnableAutoCompensation_ByDefault()
	{
		// Act
		_sut.WithAutoCompensation();

		// Assert
		_options.EnableAutoCompensation.ShouldBeTrue();
	}

	[Fact]
	public void EnableAutoCompensation_WhenTrue()
	{
		// Act
		_sut.WithAutoCompensation(true);

		// Assert
		_options.EnableAutoCompensation.ShouldBeTrue();
	}

	[Fact]
	public void DisableAutoCompensation_WhenFalse()
	{
		// Arrange
		_options.EnableAutoCompensation = true;

		// Act
		_sut.WithAutoCompensation(false);

		// Assert
		_options.EnableAutoCompensation.ShouldBeFalse();
	}

	[Fact]
	public void ReturnBuilderForChaining_WithAutoCompensation()
	{
		// Act
		var result = _sut.WithAutoCompensation();

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithStatePersistence Tests

	[Fact]
	public void EnableStatePersistence_ByDefault()
	{
		// Act
		_sut.WithStatePersistence();

		// Assert
		_options.EnableStatePersistence.ShouldBeTrue();
	}

	[Fact]
	public void DisableStatePersistence_WhenFalse()
	{
		// Arrange
		_options.EnableStatePersistence = true;

		// Act
		_sut.WithStatePersistence(false);

		// Assert
		_options.EnableStatePersistence.ShouldBeFalse();
	}

	[Fact]
	public void ReturnBuilderForChaining_WithStatePersistence()
	{
		// Act
		var result = _sut.WithStatePersistence();

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithMetrics Tests

	[Fact]
	public void EnableMetrics_ByDefault()
	{
		// Act
		_sut.WithMetrics();

		// Assert
		_options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void DisableMetrics_WhenFalse()
	{
		// Arrange
		_options.EnableMetrics = true;

		// Act
		_sut.WithMetrics(false);

		// Assert
		_options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void ReturnBuilderForChaining_WithMetrics()
	{
		// Act
		var result = _sut.WithMetrics();

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region WithCompletedSagaRetention Tests

	[Fact]
	public void SetCompletedSagaRetention()
	{
		// Arrange
		var retention = TimeSpan.FromDays(30);

		// Act
		_sut.WithCompletedSagaRetention(retention);

		// Assert
		_options.CompletedSagaRetention.ShouldBe(retention);
	}

	[Fact]
	public void ReturnBuilderForChaining_WithCompletedSagaRetention()
	{
		// Act
		var result = _sut.WithCompletedSagaRetention(TimeSpan.FromDays(7));

		// Assert
		result.ShouldBe(_sut);
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFullFluentChaining()
	{
		// Act
		var result = _sut
			.WithDefaultTimeout(TimeSpan.FromMinutes(30))
			.WithStepTimeout(TimeSpan.FromMinutes(5))
			.WithMaxRetries(5)
			.WithMaxParallelism(8)
			.WithAutoCompensation(true)
			.WithStatePersistence(true)
			.WithMetrics(true)
			.WithCompletedSagaRetention(TimeSpan.FromDays(14));

		// Assert
		result.ShouldBe(_sut);
		_options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
		_options.DefaultStepTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		_options.MaxRetryAttempts.ShouldBe(5);
		_options.MaxDegreeOfParallelism.ShouldBe(8);
		_options.EnableAutoCompensation.ShouldBeTrue();
		_options.EnableStatePersistence.ShouldBeTrue();
		_options.EnableMetrics.ShouldBeTrue();
		_options.CompletedSagaRetention.ShouldBe(TimeSpan.FromDays(14));
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Dummy saga retry policy for testing service registration.
	/// </summary>
	internal sealed class DummySagaRetryPolicy : ISagaRetryPolicy
	{
		public int MaxAttempts => 3;
		public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);

		public bool ShouldRetry(Exception exception)
			=> true;
	}

	#endregion
}
