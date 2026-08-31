// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Configuration;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineProfile"/> public class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait(TraitNames.Feature, TestFeatures.Configuration)]
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
	public void BeInternalAndSealed()
	{
		// srz528: the concrete profile is an internal implementation detail; IPipelineProfile is the
		// single public contract. The type stays sealed and is reachable here only via InternalsVisibleTo.
		// Assert
		typeof(PipelineProfile).IsPublic.ShouldBeFalse();
		typeof(PipelineProfile).IsSealed.ShouldBeTrue();
		typeof(IPipelineProfile).IsPublic.ShouldBeTrue();
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
		profile.MiddlewareEntries.ShouldNotBeNull();
		profile.MiddlewareEntries.ShouldBeEmpty();
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
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldContain(typeof(TestMiddleware));
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
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldContain(typeof(TestMiddleware));
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
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldContain(typeof(TestMiddleware));
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
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldNotContain(typeof(TestMiddleware));
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
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldNotContain(typeof(TestMiddleware));
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
		profile.MiddlewareEntries.ShouldBeEmpty();
	}

	[Fact]
	public void GetMiddlewareInOrder()
	{
		// Arrange
		var profile = new PipelineProfile("Test", MessageKinds.All);
		profile.AddMiddleware<AnotherTestMiddleware>(2);
		profile.AddMiddleware<TestMiddleware>(1);

		// Act
		var middleware = profile.MiddlewareEntries;

		// Assert
		middleware.Count.ShouldBe(2);
		middleware[0].MiddlewareType.ShouldBe(typeof(TestMiddleware));
		middleware[1].MiddlewareType.ShouldBe(typeof(AnotherTestMiddleware));
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
		profile.MiddlewareEntries.Count(e => e.MiddlewareType == typeof(TestMiddleware)).ShouldBe(1);
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

	// akwb5j: [AppliesTo(Action)] proves the profile no longer honors the attribute for kind-applicability —
	// it is returned for Event, because kind filtering is the runtime property strategy's job.
	[AppliesTo(MessageKinds.Action)]
	private sealed class ActionScopedMiddleware : IDispatchMiddleware
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
