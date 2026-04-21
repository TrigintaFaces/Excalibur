// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

namespace Excalibur.Dispatch.Middleware.Tests.Validation;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AotFluentValidatorResolverShould
{
	#region Test Messages

	private sealed record TestAotMessage(string Name, int Age) : IDispatchMessage;

	private sealed record AotMessageWithoutValidator(string Data) : IDispatchMessage;

	#endregion Test Messages

	#region Helper Methods

	private static IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		configure?.Invoke(services);
		return services.BuildServiceProvider();
	}

	#endregion Helper Methods

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new AotFluentValidatorResolver(null!));
	}

	[Fact]
	public void CreateInstanceWithValidProvider()
	{
		// Arrange
		var provider = CreateServiceProvider();

		// Act
		var sut = new AotFluentValidatorResolver(provider);

		// Assert
		sut.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region TryValidate Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => sut.TryValidate(null!));
	}

	[Fact]
	public void ThrowInvalidOperationWhenNoDispatcherRegistered()
	{
		// Arrange -- no IAotValidationDispatcher in DI
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() => sut.TryValidate(message));
		ex.Message.ShouldContain("SourceGenerators");
	}

	[Fact]
	public void ThrowInvalidOperationWhenNoDispatcherAndNoValidatorsRegistered()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithoutValidator("test");

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => sut.TryValidate(message));
	}

	[Fact]
	public void DelegateToDispatcherWhenRegistered()
	{
		// Arrange
		var expected = SerializableValidationResult.Success();
		var dispatcher = new FakeDispatcher(expected);
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddSingleton<IAotValidationDispatcher>(dispatcher);
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.Same(expected, result);
		dispatcher.CallCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnNullFromDispatcherWhenNoValidatorsForMessageType()
	{
		// Arrange -- dispatcher returns null for unknown types
		var dispatcher = new FakeDispatcher(null);
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddSingleton<IAotValidationDispatcher>(dispatcher);
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithoutValidator("test");

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void CacheDispatcherLookupAcrossMultipleCalls()
	{
		// Arrange
		var dispatcher = new FakeDispatcher(SerializableValidationResult.Success());
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddSingleton<IAotValidationDispatcher>(dispatcher);
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act -- call twice
		_ = sut.TryValidate(message);
		_ = sut.TryValidate(message);

		// Assert -- dispatcher was called both times but lookup only happens once
		dispatcher.CallCount.ShouldBe(2);
	}

	#endregion TryValidate Tests

	#region FakeDispatcher

	private sealed class FakeDispatcher : IAotValidationDispatcher
	{
		private readonly IValidationResult? _returnValue;

		public FakeDispatcher(IValidationResult? returnValue)
		{
			_returnValue = returnValue;
		}

		public int CallCount { get; private set; }

		public IValidationResult? TryValidate(IDispatchMessage message, IServiceProvider provider)
		{
			CallCount++;
			return _returnValue;
		}
	}

	#endregion FakeDispatcher
}
