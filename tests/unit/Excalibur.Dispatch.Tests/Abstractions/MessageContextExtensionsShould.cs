// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="MessageContextExtensions"/> covering property get/set,
/// well-known properties, and transport binding extensions.
/// </summary>
/// <remarks>
/// Sprint 410 - Foundation Coverage Tests (T410.6).
/// Target: Increase MessageContextExtensions coverage from 45.4% to 80%.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MessageContextExtensionsShould : IDisposable
{
	private readonly MessageEnvelope _context;

	public MessageContextExtensionsShould()
	{
		_context = new MessageEnvelope();
	}

	public void Dispose()
	{
		_context.Dispose();
	}

	#region SetProperty / GetProperty Tests

	[Fact]
	public void SetProperty_Should_Store_Value_In_Properties()
	{
		// Act
		_context.SetProperty("key1", "value1");

		// Assert
		_context.Properties.ShouldContainKey("key1");
		_context.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SetProperty_Should_Store_Typed_Value()
	{
		// Act
		_context.SetProperty("intKey", 42);
		_context.SetProperty("boolKey", true);
		_context.SetProperty("dateKey", DateTime.UtcNow.Date);

		// Assert
		_context.GetProperty<int>("intKey").ShouldBe(42);
		_context.GetProperty<bool>("boolKey").ShouldBeTrue();
		_context.GetProperty<DateTime>("dateKey").ShouldBe(DateTime.UtcNow.Date);
	}

	[Fact]
	public void GetProperty_Should_Return_Value_When_Exists()
	{
		// Arrange
		_context.SetProperty("key1", "value1");

		// Act
		var result = _context.GetProperty<string>("key1");

		// Assert
		result.ShouldBe("value1");
	}

	[Fact]
	public void GetProperty_Should_Return_Default_When_Not_Exists()
	{
		// Act
		var result = _context.GetProperty<string>("missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetProperty_Should_Return_Default_When_Type_Mismatch()
	{
		// Arrange
		_context.SetProperty("key1", "string-value");

		// Act
		var result = _context.GetProperty<int>("key1");

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void SetProperty_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		IMessageContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.SetProperty("key", "value"));
	}

	[Fact]
	public void GetProperty_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		IMessageContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.GetProperty<string>("key"));
	}

	[Fact]
	public void SetProperty_Should_Fallback_To_Items_When_Properties_Null()
	{
		// Arrange - use a context with Properties returning null
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => mockContext.Properties).Returns(null!);
		var items = new Dictionary<string, object>();
		_ = A.CallTo(() => mockContext.Items).Returns(items);

		// Act
		mockContext.SetProperty("key1", "value1");

		// Assert
		items.ShouldContainKey("key1");
		items["key1"].ShouldBe("value1");
	}

	[Fact]
	public void GetProperty_Should_Fallback_To_Items_When_Properties_Null()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => mockContext.Properties).Returns(null!);
		var items = new Dictionary<string, object> { ["key1"] = "value1" };
		_ = A.CallTo(() => mockContext.Items).Returns(items);

		// Act
		var result = mockContext.GetProperty<string>("key1");

		// Assert
		result.ShouldBe("value1");
	}

	#endregion

	#region TryGetProperty Tests

	[Fact]
	public void TryGetProperty_Should_Return_True_When_Found()
	{
		// Arrange
		_context.SetProperty("key1", "value1");

		// Act
		var found = _context.TryGetProperty<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("value1");
	}

	[Fact]
	public void TryGetProperty_Should_Return_False_When_Not_Found()
	{
		// Act
		var found = _context.TryGetProperty<string>("missing", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryGetProperty_Should_Return_False_When_Type_Mismatch()
	{
		// Arrange
		_context.SetProperty("key1", "string-value");

		// Act
		var found = _context.TryGetProperty<int>("key1", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBe(0);
	}

	[Fact]
	public void TryGetProperty_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		IMessageContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.TryGetProperty<string>("key", out _));
	}

	[Fact]
	public void TryGetProperty_Should_Fallback_To_Items()
	{
		// Arrange
		var mockContext = A.Fake<IMessageContext>();
		_ = A.CallTo(() => mockContext.Properties).Returns(null!);
		var items = new Dictionary<string, object> { ["key1"] = "value1" };
		_ = A.CallTo(() => mockContext.Items).Returns(items);

		// Act
		var found = mockContext.TryGetProperty<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("value1");
	}

	#endregion

	#region RemoveProperty Tests

	[Fact]
	public void RemoveProperty_Should_Remove_From_Properties()
	{
		// Arrange
		_context.SetProperty("key1", "value1");

		// Act
		_context.RemoveProperty("key1");

		// Assert
		_context.Properties.ShouldNotContainKey("key1");
	}

	[Fact]
	public void RemoveProperty_Should_Not_Throw_When_Not_Exists()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => _context.RemoveProperty("missing"));
	}

	[Fact]
	public void RemoveProperty_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		IMessageContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.RemoveProperty("key"));
	}

	[Fact]
	public void RemoveProperty_Should_Remove_From_Both_Properties_And_Items()
	{
		// Arrange
		_context.SetProperty("key1", "value1");
		_context.SetItem("key1", "itemValue");

		// Act
		_context.RemoveProperty("key1");

		// Assert
		_context.Properties.ShouldNotContainKey("key1");
		_context.Items.ShouldNotContainKey("key1");
	}

	#endregion

	#region TryGetValue Tests

	[Fact]
	public void TryGetValue_Should_Return_True_When_Found_In_Items()
	{
		// Arrange
		_context.SetItem("key1", "value1");

		// Act
		var found = _context.TryGetValue<string>("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe("value1");
	}

	[Fact]
	public void TryGetValue_Should_Return_False_When_Not_Found()
	{
		// Act
		var found = _context.TryGetValue<string>("missing", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public void TryGetValue_Should_Return_False_When_Type_Mismatch()
	{
		// Arrange
		_context.SetItem("key1", "string-value");

		// Act
		var found = _context.TryGetValue<int>("key1", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBe(0);
	}

	[Fact]
	public void TryGetValue_Should_Throw_When_Context_Is_Null()
	{
		// Arrange
		IMessageContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.TryGetValue<string>("key", out _));
	}

	#endregion

	#region RoutingDecision Property Tests

	[Fact]
	public void RoutingDecision_Getter_Should_Return_Context_RoutingDecision()
	{
		// Arrange
		var routingDecision = RoutingDecision.Success("local", []);
		_context.RoutingDecision = routingDecision;

		// Act
		var result = _context.RoutingDecision;

		// Assert
		result.ShouldBe(routingDecision);
	}

	[Fact]
	public void RoutingDecision_Setter_Should_Set_Context_RoutingDecision()
	{
		// Arrange
		var routingDecision = RoutingDecision.Success("rabbitmq", ["billing-service"]);

		// Act
		_context.RoutingDecision = routingDecision;

		// Assert
		_context.RoutingDecision.ShouldBe(routingDecision);
	}

	#endregion

	#region ValidationResult Extension Tests

	[Fact]
	public void ValidationResult_Should_Get_And_Set_Value()
	{
		// Arrange
		var validationResult = new object();

		// Act
		_context.ValidationResult(validationResult);

		// Assert
		_context.ValidationResult().ShouldBe(validationResult);
	}

	[Fact]
	public void ValidationResult_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.ValidationResult().ShouldBeNull();
	}

	#endregion

	#region AuthorizationResult Extension Tests

	[Fact]
	public void AuthorizationResult_Should_Get_And_Set_Value()
	{
		// Arrange
		var authResult = new object();

		// Act
		_context.AuthorizationResult(authResult);

		// Assert
		_context.AuthorizationResult().ShouldBe(authResult);
	}

	[Fact]
	public void AuthorizationResult_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.AuthorizationResult().ShouldBeNull();
	}

	#endregion

	#region VersionMetadata Extension Tests

	[Fact]
	public void VersionMetadata_Should_Get_And_Set_Value()
	{
		// Arrange
		var versionMetadata = new object();

		// Act
		_context.VersionMetadata(versionMetadata);

		// Assert
		_context.VersionMetadata().ShouldBe(versionMetadata);
	}

	[Fact]
	public void VersionMetadata_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.VersionMetadata().ShouldBeNull();
	}

	#endregion

	#region DesiredVersion Extension Tests

	[Fact]
	public void DesiredVersion_Should_Get_And_Set_Value()
	{
		// Act
		_context.DesiredVersion("2.0");

		// Assert
		_context.DesiredVersion().ShouldBe("2.0");
	}

	[Fact]
	public void DesiredVersion_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.DesiredVersion().ShouldBeNull();
	}

	#endregion

	#region MessageVersion Extension Tests

	[Fact]
	public void MessageVersion_Should_Get_And_Set_Value()
	{
		// Act
		_context.MessageVersion("1.5");

		// Assert
		_context.MessageVersion().ShouldBe("1.5");
	}

	[Fact]
	public void MessageVersion_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.MessageVersion().ShouldBeNull();
	}

	#endregion

	#region SerializerVersion Extension Tests

	[Fact]
	public void SerializerVersion_Should_Get_And_Set_Value()
	{
		// Act
		_context.SerializerVersion("3.0");

		// Assert
		_context.SerializerVersion().ShouldBe("3.0");
	}

	[Fact]
	public void SerializerVersion_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.SerializerVersion().ShouldBeNull();
	}

	#endregion

	#region ContractVersion Extension Tests

	[Fact]
	public void ContractVersion_Should_Get_And_Set_Value()
	{
		// Act
		_context.ContractVersion("4.0");

		// Assert
		_context.ContractVersion().ShouldBe("4.0");
	}

	[Fact]
	public void ContractVersion_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.ContractVersion().ShouldBeNull();
	}

	#endregion

	#region PartitionKey Extension Tests

	[Fact]
	public void PartitionKey_Should_Get_And_Set_Value()
	{
		// Act
		_context.PartitionKey("partition-123");

		// Assert
		_context.PartitionKey().ShouldBe("partition-123");
	}

	[Fact]
	public void PartitionKey_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.PartitionKey().ShouldBeNull();
	}

	#endregion

	#region ReplyTo Extension Tests

	[Fact]
	public void ReplyTo_Should_Get_And_Set_Value()
	{
		// Act
		_context.ReplyTo("reply-queue");

		// Assert
		_context.ReplyTo().ShouldBe("reply-queue");
	}

	[Fact]
	public void ReplyTo_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.ReplyTo().ShouldBeNull();
	}

	#endregion

	#region Metadata Extension Tests

	[Fact]
	public void Metadata_Should_Get_And_Set_Value()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["key1"] = "value1" };

		// Act
		_context.Metadata(metadata);

		// Assert
		var result = _context.Metadata();
		_ = result.ShouldNotBeNull();
		result.ShouldContainKey("key1");
		result["key1"].ShouldBe("value1");
	}

	[Fact]
	public void Metadata_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.Metadata().ShouldBeNull();
	}

	#endregion

	#region TransportBinding Extension Tests

	[Fact]
	public void TransportBinding_Should_Return_Value_When_Set()
	{
		// Arrange
		var transportBinding = A.Fake<ITransportBinding>();
		_context.SetProperty("Excalibur.Dispatch.TransportBinding", transportBinding);

		// Act
		var result = _context.TransportBinding();

		// Assert
		result.ShouldBe(transportBinding);
	}

	[Fact]
	public void TransportBinding_Should_Return_Null_When_Not_Set()
	{
		// Act & Assert
		_context.TransportBinding().ShouldBeNull();
	}

	[Fact]
	public void HasTransportBinding_Should_Return_True_When_Set()
	{
		// Arrange
		var transportBinding = A.Fake<ITransportBinding>();
		_context.SetProperty("Excalibur.Dispatch.TransportBinding", transportBinding);

		// Act & Assert
		_context.HasTransportBinding().ShouldBeTrue();
	}

	[Fact]
	public void HasTransportBinding_Should_Return_False_When_Not_Set()
	{
		// Act & Assert
		_context.HasTransportBinding().ShouldBeFalse();
	}

	#endregion
}
