// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;
using Excalibur.Domain.Concurrency;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Application;

/// <summary>
/// Unit tests for <see cref="ActivityContext"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Context")]
public sealed class ActivityContextShould : UnitTestBase
{
	private readonly ITenantId _tenantId;
	private readonly ICorrelationId _correlationId;
	private readonly IETag _eTag;
	private readonly IConfiguration _configuration;
	private readonly IClientAddress _clientAddress;
	private readonly IServiceProvider _serviceProvider;

	public ActivityContextShould()
	{
		_tenantId = A.Fake<ITenantId>();
		_correlationId = A.Fake<ICorrelationId>();
		_eTag = A.Fake<IETag>();
		_configuration = A.Fake<IConfiguration>();
		_clientAddress = A.Fake<IClientAddress>();
		_serviceProvider = A.Fake<IServiceProvider>();
	}

	private ActivityContext CreateContext()
	{
		return new ActivityContext(
			_tenantId,
			_correlationId,
			_eTag,
			_configuration,
			_clientAddress,
			_serviceProvider);
	}

	#region Constructor Tests

	[Fact]
	public void Create_WithDependencies_StoresInjectedValues()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.GetValue<ITenantId>("TenantId", null!).ShouldBe(_tenantId);
		context.GetValue<ICorrelationId>("CorrelationId", null!).ShouldBe(_correlationId);
		context.GetValue<IETag>("ETag", null!).ShouldBe(_eTag);
		context.GetValue<IConfiguration>("IConfiguration", null!).ShouldBe(_configuration);
		context.GetValue<IClientAddress>("ClientAddress", null!).ShouldBe(_clientAddress);
		context.GetValue<IServiceProvider>("IServiceProvider", null!).ShouldBe(_serviceProvider);
	}

	#endregion

	#region GetValue Tests

	[Fact]
	public void GetValue_ExistingKey_ReturnsValue()
	{
		// Arrange
		var context = CreateContext();
		context.SetValue("TestKey", "TestValue");

		// Act
		var result = context.GetValue("TestKey", "Default");

		// Assert
		result.ShouldBe("TestValue");
	}

	[Fact]
	public void GetValue_NonExistingKey_ReturnsDefault()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var result = context.GetValue("NonExistingKey", "DefaultValue");

		// Assert
		result.ShouldBe("DefaultValue");
	}

	[Fact]
	public void GetValue_InjectedValue_ReturnsInjectedValue()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var result = context.GetValue<ITenantId>("TenantId", null!);

		// Assert
		result.ShouldBe(_tenantId);
	}

	[Fact]
	public void GetValue_WithIntType_ReturnsCorrectType()
	{
		// Arrange
		var context = CreateContext();
		context.SetValue("Counter", 42);

		// Act
		var result = context.GetValue("Counter", 0);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public void GetValue_WithComplexType_ReturnsCorrectType()
	{
		// Arrange
		var context = CreateContext();
		var testObject = new TestContextData { Name = "Test", Value = 100 };
		context.SetValue("Data", testObject);

		// Act
		var result = context.GetValue<TestContextData>("Data", null!);

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("Test");
		result.Value.ShouldBe(100);
	}

	#endregion

	#region SetValue Tests

	[Fact]
	public void SetValue_NewKey_AddsValue()
	{
		// Arrange
		var context = CreateContext();

		// Act
		context.SetValue("NewKey", "NewValue");

		// Assert
		context.GetValue<string>("NewKey", null!).ShouldBe("NewValue");
	}

	[Fact]
	public void SetValue_ExistingKey_UpdatesValue()
	{
		// Arrange
		var context = CreateContext();
		context.SetValue("Key", "Value1");

		// Act
		context.SetValue("Key", "Value2");

		// Assert
		context.GetValue<string>("Key", null!).ShouldBe("Value2");
	}

	[Fact]
	public void SetValue_NullValue_ThrowsArgumentNullException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => context.SetValue<string>("Key", null!));
	}

	[Theory]
	[InlineData("TenantId")]
	[InlineData("CorrelationId")]
	[InlineData("ETag")]
	[InlineData("IConfiguration")]
	[InlineData("ClientAddress")]
	[InlineData("IServiceProvider")]
	public void SetValue_InjectedValueKey_ThrowsInvalidOperationException(string key)
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => context.SetValue(key, "NewValue"));
		exception.Message.ShouldContain(key);
		exception.Message.ShouldContain("injected value");
	}

	[Theory]
	[InlineData("tenantid")]
	[InlineData("TENANTID")]
	[InlineData("TenantID")]
	public void SetValue_InjectedValueKey_CaseInsensitive_ThrowsInvalidOperationException(string key)
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => context.SetValue(key, "NewValue"));
	}

	#endregion

	#region ContainsKey Tests

	[Fact]
	public void ContainsKey_ExistingKey_ReturnsTrue()
	{
		// Arrange
		var context = CreateContext();
		context.SetValue("ExistingKey", "Value");

		// Act
		var result = context.ContainsKey("ExistingKey");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsKey_NonExistingKey_ReturnsFalse()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var result = context.ContainsKey("NonExistingKey");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsKey_InjectedValue_ReturnsTrue()
	{
		// Arrange
		var context = CreateContext();

		// Act
		var result = context.ContainsKey("TenantId");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsKey_NullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.ContainsKey(null!));
	}

	[Fact]
	public void ContainsKey_EmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.ContainsKey(string.Empty));
	}

	[Fact]
	public void ContainsKey_WhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.ContainsKey("   "));
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_ExistingKey_RemovesValue()
	{
		// Arrange
		var context = CreateContext();
		context.SetValue("KeyToRemove", "Value");

		// Act
		context.Remove("KeyToRemove");

		// Assert
		context.ContainsKey("KeyToRemove").ShouldBeFalse();
	}

	[Fact]
	public void Remove_NonExistingKey_DoesNotThrow()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert - should not throw
		Should.NotThrow(() => context.Remove("NonExistingKey"));
	}

	[Theory]
	[InlineData("TenantId")]
	[InlineData("CorrelationId")]
	[InlineData("ETag")]
	[InlineData("IConfiguration")]
	[InlineData("ClientAddress")]
	[InlineData("IServiceProvider")]
	public void Remove_InjectedValueKey_ThrowsInvalidOperationException(string key)
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => context.Remove(key));
		exception.Message.ShouldContain(key);
		exception.Message.ShouldContain("injected value");
	}

	[Fact]
	public void Remove_NullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.Remove(null!));
	}

	[Fact]
	public void Remove_EmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = CreateContext();

		// Act & Assert
		Should.Throw<ArgumentException>(() => context.Remove(string.Empty));
	}

	#endregion

	#region IActivityContext Interface

	[Fact]
	public void ImplementsIActivityContext()
	{
		// Arrange & Act
		var context = CreateContext();

		// Assert
		context.ShouldBeAssignableTo<IActivityContext>();
	}

	#endregion

	#region Test Helper Class

	private sealed class TestContextData
	{
		public string Name { get; init; } = string.Empty;
		public int Value { get; init; }
	}

	#endregion
}
