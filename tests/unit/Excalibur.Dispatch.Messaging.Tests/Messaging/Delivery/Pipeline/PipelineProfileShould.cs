// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using PipelineProfile = Excalibur.Dispatch.Delivery.Pipeline.PipelineProfile;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for the <see cref="PipelineProfile"/> class.
/// </summary>
/// <remarks>
/// Sprint 460 - Task S460.4: Middleware Pipeline Tests.
/// Tests the pipeline profile middleware composition and message compatibility.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class PipelineProfileShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange
		var middlewareTypes = new[] { typeof(TestMiddleware) };

		// Act
		var profile = new PipelineProfile(
			"TestProfile",
			"Test description",
			middlewareTypes,
			isStrict: true,
			supportedMessageKinds: MessageKinds.Action);

		// Assert
		profile.Name.ShouldBe("TestProfile");
		profile.Description.ShouldBe("Test description");
		profile.MiddlewareTypes.Count.ShouldBe(1);
		profile.IsStrict.ShouldBeTrue();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void Constructor_WithEmptyMiddleware_CreatesEmptyProfile()
	{
		// Act
		var profile = new PipelineProfile(
			"EmptyProfile",
			"Empty middleware list",
			Array.Empty<Type>());

		// Assert
		profile.MiddlewareTypes.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_ThrowsOnNullName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PipelineProfile(null!, "description", Array.Empty<Type>()));
	}

	[Fact]
	public void Constructor_ThrowsOnEmptyName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PipelineProfile("", "description", Array.Empty<Type>()));
	}

	[Fact]
	public void Constructor_ThrowsOnWhitespaceName()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PipelineProfile("   ", "description", Array.Empty<Type>()));
	}

	[Fact]
	public void Constructor_ThrowsOnNullDescription()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PipelineProfile("Name", null!, Array.Empty<Type>()));
	}

	[Fact]
	public void Constructor_ThrowsOnNullMiddlewareTypes()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PipelineProfile("Name", "description", null!));
	}

	[Fact]
	public void Constructor_ThrowsOnInvalidMiddlewareType()
	{
		// Arrange - string does not implement IDispatchMiddleware
		var invalidTypes = new[] { typeof(string) };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new PipelineProfile("Name", "description", invalidTypes));
	}

	#endregion

	#region Static Factory Methods Tests

	[Fact]
	public void CreateStrictProfile_HasExpectedConfiguration()
	{
		// Act
		var profile = PipelineProfile.CreateStrictProfile();

		// Assert
		profile.Name.ShouldBe("Strict");
		profile.IsStrict.ShouldBeTrue();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Action);
		profile.MiddlewareTypes.ShouldNotBeEmpty();
	}

	[Fact]
	public void CreateInternalEventProfile_HasExpectedConfiguration()
	{
		// Act
		var profile = PipelineProfile.CreateInternalEventProfile();

		// Assert
		profile.Name.ShouldBe("InternalEvent");
		profile.IsStrict.ShouldBeFalse();
		profile.SupportedMessageKinds.ShouldBe(MessageKinds.Event);
		profile.MiddlewareTypes.ShouldBeEmpty(); // Zero middleware overhead
	}

	#endregion

	#region IsCompatible Tests

	[Fact]
	public void IsCompatible_WithAllMessageKinds_ReturnsTrue()
	{
		// Arrange
		var profile = new PipelineProfile(
			"AllKinds",
			"Supports all kinds",
			Array.Empty<Type>(),
			supportedMessageKinds: MessageKinds.All);
		var message = new TestActionMessage();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsCompatible_WithMatchingKind_ReturnsTrue()
	{
		// Arrange
		var profile = new PipelineProfile(
			"ActionOnly",
			"Actions only",
			Array.Empty<Type>(),
			supportedMessageKinds: MessageKinds.Action);
		var message = new TestActionMessage();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsCompatible_WithNonMatchingKind_ReturnsFalse()
	{
		// Arrange
		var profile = new PipelineProfile(
			"EventOnly",
			"Events only",
			Array.Empty<Type>(),
			supportedMessageKinds: MessageKinds.Event);
		var message = new TestActionMessage();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsCompatible_ThrowsOnNullMessage()
	{
		// Arrange
		var profile = new PipelineProfile(
			"Test",
			"Test",
			Array.Empty<Type>());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			profile.IsCompatible(null!));
	}

	[Fact]
	public void IsCompatible_WithEventMessage_ReturnsTrue_ForEventProfile()
	{
		// Arrange
		var profile = new PipelineProfile(
			"EventProfile",
			"Events",
			Array.Empty<Type>(),
			supportedMessageKinds: MessageKinds.Event);
		var message = new TestEventMessage();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsCompatible_WithDocumentMessage_ReturnsTrue_ForDocumentProfile()
	{
		// Arrange
		var profile = new PipelineProfile(
			"DocumentProfile",
			"Documents",
			Array.Empty<Type>(),
			supportedMessageKinds: MessageKinds.Document);
		var message = new TestDocumentMessage();

		// Act
		var result = profile.IsCompatible(message);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region GetApplicableMiddleware Tests

	[Fact]
	public void GetApplicableMiddleware_ReturnsAllMiddleware_WhenNoFiltering()
	{
		// Arrange
		var middlewareTypes = new[] { typeof(TestMiddleware), typeof(TestMiddleware2) };
		var profile = new PipelineProfile(
			"Test",
			"Test",
			middlewareTypes);

		// Act
		var applicable = profile.GetApplicableMiddleware(MessageKinds.Action);

		// Assert
		applicable.Count.ShouldBe(2);
	}

	[Fact]
	public void GetApplicableMiddleware_ReturnsEmptyList_WhenNoMiddleware()
	{
		// Arrange
		var profile = new PipelineProfile(
			"Empty",
			"Empty",
			Array.Empty<Type>());

		// Act
		var applicable = profile.GetApplicableMiddleware(MessageKinds.Action);

		// Assert
		applicable.ShouldBeEmpty();
	}

	[Fact]
	public void GetApplicableMiddleware_WithFeatures_ReturnsFilteredList()
	{
		// Arrange
		var middlewareTypes = new[] { typeof(TestMiddleware) };
		var profile = new PipelineProfile(
			"Test",
			"Test",
			middlewareTypes);
		var enabledFeatures = new HashSet<DispatchFeatures>();

		// Act
		var applicable = profile.GetApplicableMiddleware(MessageKinds.Action, enabledFeatures);

		// Assert
		_ = applicable.ShouldNotBeNull();
	}

	#endregion

	#region IPipelineProfile Interface Tests

	[Fact]
	public void ImplementsIPipelineProfile()
	{
		// Arrange
		var profile = new PipelineProfile(
			"Test",
			"Test",
			Array.Empty<Type>());

		// Assert
		_ = profile.ShouldBeAssignableTo<IPipelineProfile>();
	}

	#endregion

	#region Test Fixtures

#pragma warning disable CA1034 // Nested types should not be visible

	public sealed class TestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);
	}

	public sealed class TestMiddleware2 : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
			=> nextDelegate(message, context, cancellationToken);
	}

	public sealed class TestActionMessage : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestActionMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestEventMessage : IDispatchEvent
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Event;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestEventMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	public sealed class TestDocumentMessage : IDispatchDocument
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Document;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDocumentMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
		public string DocumentId { get; set; } = Guid.NewGuid().ToString();
		public string DocumentType { get; set; } = "Test";
		public string? ContentType { get; set; } = "application/json";
	}

#pragma warning restore CA1034

	#endregion
}
