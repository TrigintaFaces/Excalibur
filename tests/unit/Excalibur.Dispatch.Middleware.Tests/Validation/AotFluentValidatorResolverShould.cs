// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Validation;
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
	public async Task ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () => await sut.TryValidateAsync(null!));
	}

	[Fact]
	public async Task ThrowInvalidOperationWhenNoDispatcherRegistered()
	{
		// Arrange -- no IAotValidationDispatcher in DI
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () => await sut.TryValidateAsync(message));
		ex.Message.ShouldContain("SourceGenerators");
	}

	[Fact]
	public async Task ThrowInvalidOperationWhenNoDispatcherAndNoValidatorsRegistered()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithoutValidator("test");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () => await sut.TryValidateAsync(message));
	}

	[Fact]
	public async Task DelegateToDispatcherWhenRegistered()
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
		var result = await sut.TryValidateAsync(message);

		// Assert
		Assert.Same(expected, result);
		dispatcher.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task ReturnNullFromDispatcherWhenNoValidatorsForMessageType()
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
		var result = await sut.TryValidateAsync(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public async Task InvokeRegisteredDispatcherOnEveryCall()
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
		_ = await sut.TryValidateAsync(message);
		_ = await sut.TryValidateAsync(message);

		// Assert -- the registered dispatcher was called both times
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

		public ValueTask<IValidationResult?> TryValidateAsync(IDispatchMessage message, IServiceProvider provider, CancellationToken cancellationToken = default)
		{
			CallCount++;
			return new(_returnValue);
		}
	}

	#endregion FakeDispatcher
}
