// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineProfile"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
[Trait("Feature", "Configuration")]
public sealed class PipelineProfileShould
{
	[Fact]
	public void ImplementIPipelineProfile()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Assert
		profile.ShouldBeAssignableTo<IPipelineProfile>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(PipelineProfile).IsPublic.ShouldBeTrue();
		typeof(PipelineProfile).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineProfile(null!, MessageKinds.All));
	}

	[Fact]
	public void ThrowWhenNameIsEmpty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineProfile(string.Empty, MessageKinds.All));
	}

	[Fact]
	public void ThrowWhenNameIsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PipelineProfile("   ", MessageKinds.All));
	}

	[Fact]
	public void StoreNameProperty()
	{
		// Arrange & Act
		var profile = new PipelineProfile("TestProfile", MessageKinds.All);

		// Assert
		profile.Name.ShouldBe("TestProfile");
	}

	[Fact]
	public void StoreSupportedKindsProperty()
	{
		// Arrange & Act
		var profile = new PipelineProfile("Test", MessageKinds.Action);

		// Assert
		profile.SupportedKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void HaveSupportedMessageKindsMatchSupportedKinds()
	{
		// Arrange & Act
		var profile = new PipelineProfile("Test", MessageKinds.Event);

		// Assert
		profile.SupportedMessageKinds.ShouldBe(profile.SupportedKinds);
	}

	[Fact]
	public void InitializeWithEmptyMiddlewareList()
	{
		// Arrange & Act
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Assert
		profile.MiddlewareTypes.ShouldNotBeNull();
		profile.MiddlewareTypes.ShouldBeEmpty();
	}

	[Fact]
	public void InitializeIsStrictAsFalseByDefault()
	{
		// Arrange & Act
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Assert
		profile.IsStrict.ShouldBeFalse();
	}

	[Fact]
	public void InitializeDescriptionAsEmptyByDefault()
	{
		// Arrange & Act
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Assert
		profile.Description.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingIsStrict()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act
		profile.IsStrict = true;

		// Assert
		profile.IsStrict.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingDescription()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act
		profile.Description = "Test description";

		// Assert
		profile.Description.ShouldBe("Test description");
	}

	[Fact]
	public void ConstructWithFullConfigurationParameters()
	{
		// Arrange
		var middlewareTypes = new[] { typeof(TestMiddleware) };

		// Act
		var profile = new PipelineProfile(
			"FullConfig",
			"Full configuration description",
			middlewareTypes,
			isStrict: true,
			supportedMessageKinds: MessageKinds.Action);

		// Assert
		profile.Name.ShouldBe("FullConfig");
		profile.Description.ShouldBe("Full configuration description");
		profile.IsStrict.ShouldBeTrue();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Action);
		profile.MiddlewareTypes.ShouldContain(typeof(TestMiddleware));
	}

	[Fact]
	public void ThrowWhenMiddlewareTypesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new PipelineProfile("Test", "Desc", null!, true, MessageKinds.All));
	}

	[Fact]
	public void AddMiddlewareViaGenericMethod()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act
		profile.AddMiddleware<TestMiddleware>(0);

		// Assert
		profile.MiddlewareTypes.ShouldContain(typeof(TestMiddleware));
	}

	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "Testing non-generic overload")]
	public void AddMiddlewareViaTypeMethod()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act
		profile.AddMiddleware(typeof(TestMiddleware), 0);

		// Assert
		profile.MiddlewareTypes.ShouldContain(typeof(TestMiddleware));
	}

	[Fact]
	public void ThrowWhenAddMiddlewareTypeIsNull()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			profile.AddMiddleware(null!, 0));
	}

	[Fact]
	public void ThrowWhenAddMiddlewareTypeDoesNotImplementInterface()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			profile.AddMiddleware(typeof(string), 0));
	}

	[Fact]
	public void RemoveMiddlewareViaGenericMethod()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<TestMiddleware>(0);

		// Act
		profile.RemoveMiddleware<TestMiddleware>();

		// Assert
		profile.MiddlewareTypes.ShouldNotContain(typeof(TestMiddleware));
	}

	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "Testing non-generic overload")]
	public void RemoveMiddlewareViaTypeMethod()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware(typeof(TestMiddleware), 0);

		// Act
		profile.RemoveMiddleware(typeof(TestMiddleware));

		// Assert
		profile.MiddlewareTypes.ShouldNotContain(typeof(TestMiddleware));
	}

	[Fact]
	public void HandleRemovingNonExistentMiddleware()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act & Assert - Should not throw
		Should.NotThrow(() => profile.RemoveMiddleware<TestMiddleware>());
	}

	[Fact]
	public void ClearAllMiddleware()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<TestMiddleware>(0);
		profile.AddMiddleware<AnotherTestMiddleware>(1);

		// Act
		profile.ClearMiddleware();

		// Assert
		profile.MiddlewareTypes.ShouldBeEmpty();
	}

	[Fact]
	public void GetMiddlewareInOrder()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<AnotherTestMiddleware>(2);
		profile.AddMiddleware<TestMiddleware>(1);

		// Act
		var middleware = profile.GetMiddleware();

		// Assert
		middleware.Count.ShouldBe(2);
		middleware[0].ShouldBe(typeof(TestMiddleware));
		middleware[1].ShouldBe(typeof(AnotherTestMiddleware));
	}

	[Fact]
	public void GetApplicableMiddlewareForMessageKind()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<TestMiddleware>(0);

		// Act
		var middleware = profile.GetApplicableMiddleware(MessageKinds.Action);

		// Assert
		middleware.ShouldNotBeEmpty();
	}

	[Fact]
	public void GetApplicableMiddlewareWithEnabledFeatures()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<TestMiddleware>(0);
		var enabledFeatures = new HashSet<DispatchFeatures> { DispatchFeatures.Validation };

		// Act
		var middleware = profile.GetApplicableMiddleware(MessageKinds.Action, enabledFeatures);

		// Assert
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenGetApplicableMiddlewareEnabledFeaturesIsNull()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			profile.GetApplicableMiddleware(MessageKinds.Action, null!));
	}

	[Fact]
	public void CreateStrictProfile()
	{
		// Act
		var profile = PipelineProfile.CreateStrictProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe("Strict");
		profile.IsStrict.ShouldBeTrue();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void CreateInternalEventProfile()
	{
		// Act
		var profile = PipelineProfile.CreateInternalEventProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe("InternalEvent");
		profile.IsStrict.ShouldBeFalse();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void NotAddDuplicateMiddleware()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act
		profile.AddMiddleware<TestMiddleware>(0);
		profile.AddMiddleware<TestMiddleware>(1); // Duplicate

		// Assert
		profile.MiddlewareTypes.Count(t => t == typeof(TestMiddleware)).ShouldBe(1);
	}

	[Fact]
	public void IsCompatibleReturnsTrueForMatchingMessageKind()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.Action);
		var message = A.Fake<IDispatchAction<string>>();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenIsCompatibleMessageIsNull()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			profile.IsCompatible(null!));
	}

	/// <summary>
	/// Test middleware implementation.
	/// </summary>
	private sealed class TestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			nextDelegate(message, context, cancellationToken);
	}

	/// <summary>
	/// Another test middleware implementation.
	/// </summary>
	private sealed class AnotherTestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) =>
			nextDelegate(message, context, cancellationToken);
	}
}
