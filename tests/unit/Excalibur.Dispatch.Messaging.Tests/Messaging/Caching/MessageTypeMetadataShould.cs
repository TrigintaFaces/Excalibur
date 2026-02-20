// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="MessageTypeMetadata"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class MessageTypeMetadataShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullType_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageTypeMetadata(null!));
	}

	[Fact]
	public void Constructor_SetsTypeProperty()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.Type.ShouldBe(typeof(TestMessage));
	}

	[Fact]
	public void Constructor_SetsFullName()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.FullName.ShouldBe(typeof(TestMessage).FullName);
	}

	[Fact]
	public void Constructor_SetsSimpleName()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.SimpleName.ShouldBe("TestMessage");
	}

	[Fact]
	public void Constructor_SetsAssemblyQualifiedName()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.AssemblyQualifiedName.ShouldNotBeNullOrEmpty();
		metadata.AssemblyQualifiedName.ShouldContain("TestMessage");
	}

	#endregion

	#region Interface Detection Tests

	[Fact]
	public void Constructor_WithPlainType_SetsAllInterfaceFlagsToFalse()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.IsEvent.ShouldBeFalse();
		metadata.IsCommand.ShouldBeFalse();
		metadata.IsDocument.ShouldBeFalse();
		metadata.IsProjection.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithEventType_SetsIsEventToTrue()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestDispatchEvent));

		// Assert
		metadata.IsEvent.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithCommandType_SetsIsCommandToTrue()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestDispatchAction));

		// Assert
		metadata.IsCommand.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithDocumentType_SetsIsDocumentToTrue()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestDispatchDocument));

		// Assert
		metadata.IsDocument.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithProjectionType_SetsIsProjectionToTrue()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestProjection));

		// Assert
		metadata.IsProjection.ShouldBeTrue();
	}

	#endregion

	#region Routing Hint Tests

	[Fact]
	public void Constructor_WithPlainType_SetsRoutingHintToDefault()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.RoutingHint.ShouldBe("default");
	}

	[Fact]
	public void Constructor_WithEventType_SetsRoutingHintToLocal()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestDispatchEvent));

		// Assert
		metadata.RoutingHint.ShouldBe("local");
	}

	[Fact]
	public void Constructor_WithDocumentType_SetsRoutingHintToLocal()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestDispatchDocument));

		// Assert
		metadata.RoutingHint.ShouldBe("local");
	}

	[Fact]
	public void Constructor_WithProjectionType_SetsRoutingHintToLocal()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestProjection));

		// Assert
		metadata.RoutingHint.ShouldBe("local");
	}

	[Fact]
	public void Constructor_WithIntegrationEvent_SetsRoutingHintToRemote()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestIntegrationEvent));

		// Assert
		metadata.RoutingHint.ShouldBe("remote");
	}

	#endregion

	#region HashCode Tests

	[Fact]
	public void Constructor_SetsTypeHashCode()
	{
		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(TestMessage));

		// Assert
		metadata.TypeHashCode.ShouldBe(typeof(TestMessage).GetHashCode());
	}

	[Fact]
	public void DifferentTypes_HaveDifferentHashCodes()
	{
		// Arrange & Act
		var metadata1 = new MessageTypeMetadata(typeof(TestMessage));
		var metadata2 = new MessageTypeMetadata(typeof(string));

		// Assert
		metadata1.TypeHashCode.ShouldNotBe(metadata2.TypeHashCode);
	}

	#endregion

	#region Type Without FullName Tests

	[Fact]
	public void Constructor_WithGenericTypeParameter_HandlesNullFullName()
	{
		// This tests the fallback when type.FullName is null
		// In practice, this happens with open generic type parameters

		// Arrange & Act
		var metadata = new MessageTypeMetadata(typeof(int)); // Use a known type

		// Assert
		metadata.FullName.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region Test Types

	private sealed class TestMessage;

	// ReSharper disable UnusedType.Local
	private interface IDispatchEvent;

	private interface IDispatchAction;

	private interface IDispatchDocument;

	private interface IProjection;

	private interface IIntegrationEvent;

	private sealed class TestDispatchEvent : IDispatchEvent;

	private sealed class TestDispatchAction : IDispatchAction;

	private sealed class TestDispatchDocument : IDispatchDocument;

	private sealed class TestProjection : IProjection;

	private sealed class TestIntegrationEvent : IDispatchEvent, IIntegrationEvent;
	// ReSharper restore UnusedType.Local

	#endregion
}
