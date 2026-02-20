// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for the <see cref="ActivityContextExtensions"/> class in Excalibur.A3.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityContextExtensionsShould
{
	private readonly IActivityContext _activityContext = A.Fake<IActivityContext>();

	#region AccessToken Tests

	[Fact]
	public void AccessTokenShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.AccessToken());
	}

	[Fact]
	public void AccessTokenShouldReturnNullWhenNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("AccessToken", default(IAccessToken)))
			.Returns(null);

		// Act
		var result = _activityContext.AccessToken();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void AccessTokenShouldReturnTokenWhenPresent()
	{
		// Arrange
		var expectedToken = A.Fake<IAccessToken>();
		_ = A.CallTo(() => _activityContext.GetValue("AccessToken", default(IAccessToken)))
			.Returns(expectedToken);

		// Act
		var result = _activityContext.AccessToken();

		// Assert
		result.ShouldBe(expectedToken);
	}

	#endregion

	#region UserId Tests

	[Fact]
	public void UserIdShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.UserId());
	}

	[Fact]
	public void UserIdShouldReturnNullWhenAccessTokenNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("AccessToken", default(IAccessToken)))
			.Returns(null);

		// Act
		var result = _activityContext.UserId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void UserIdShouldReturnUserIdFromAccessToken()
	{
		// Arrange
		const string expectedUserId = "user123";
		var accessToken = A.Fake<IAccessToken>();
		_ = A.CallTo(() => accessToken.UserId).Returns(expectedUserId);
		_ = A.CallTo(() => _activityContext.GetValue("AccessToken", default(IAccessToken)))
			.Returns(accessToken);

		// Act
		var result = _activityContext.UserId();

		// Assert
		result.ShouldBe(expectedUserId);
	}

	#endregion

	#region ApplicationName Tests

	[Fact]
	public void ApplicationNameShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ApplicationName());
	}

	[Fact]
	public void ApplicationNameShouldReturnNullWhenConfigurationNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("IConfiguration", default(IConfiguration)))
			.Returns(null);

		// Act
		var result = _activityContext.ApplicationName();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ApplicationNameShouldReturnApplicationNameFromConfiguration()
	{
		// Arrange
		const string expectedName = "TestApp";
		var config = A.Fake<IConfiguration>();
		_ = A.CallTo(() => config["ApplicationName"]).Returns(expectedName);
		_ = A.CallTo(() => _activityContext.GetValue("IConfiguration", default(IConfiguration)))
			.Returns(config);

		// Act
		var result = _activityContext.ApplicationName();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region ClientAddress Tests

	[Fact]
	public void ClientAddressShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ClientAddress());
	}

	[Fact]
	public void ClientAddressShouldReturnNullWhenNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("ClientAddress", default(IClientAddress)))
			.Returns(null);

		// Act
		var result = _activityContext.ClientAddress();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ClientAddressShouldReturnAddressWhenPresent()
	{
		// Arrange
		const string expectedAddress = "192.168.1.100";
		var clientAddress = A.Fake<IClientAddress>();
		_ = A.CallTo(() => clientAddress.Value).Returns(expectedAddress);
		_ = A.CallTo(() => _activityContext.GetValue("ClientAddress", default(IClientAddress)))
			.Returns(clientAddress);

		// Act
		var result = _activityContext.ClientAddress();

		// Assert
		result.ShouldBe(expectedAddress);
	}

	#endregion

	#region CorrelationId Tests

	[Fact]
	public void CorrelationIdShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.CorrelationId());
	}

	[Fact]
	public void CorrelationIdShouldReturnNullWhenNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("CorrelationId", default(ICorrelationId)))
			.Returns(null);

		// Act
		var result = _activityContext.CorrelationId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void CorrelationIdShouldReturnGuidWhenPresent()
	{
		// Arrange
		var expectedGuid = Guid.NewGuid();
		var correlationId = A.Fake<ICorrelationId>();
		_ = A.CallTo(() => correlationId.Value).Returns(expectedGuid);
		_ = A.CallTo(() => _activityContext.GetValue("CorrelationId", default(ICorrelationId)))
			.Returns(correlationId);

		// Act
		var result = _activityContext.CorrelationId();

		// Assert
		result.ShouldBe(expectedGuid);
	}

	#endregion

	#region TenantId Tests

	[Fact]
	public void TenantIdShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.TenantId());
	}

	[Fact]
	public void TenantIdShouldReturnNullWhenNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("TenantId", default(ITenantId)))
			.Returns(null);

		// Act
		var result = _activityContext.TenantId();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void TenantIdShouldReturnTenantIdWhenPresent()
	{
		// Arrange
		const string expectedTenantId = "tenant-abc";
		var tenantId = A.Fake<ITenantId>();
		_ = A.CallTo(() => tenantId.Value).Returns(expectedTenantId);
		_ = A.CallTo(() => _activityContext.GetValue("TenantId", default(ITenantId)))
			.Returns(tenantId);

		// Act
		var result = _activityContext.TenantId();

		// Assert
		result.ShouldBe(expectedTenantId);
	}

	#endregion

	#region Get<T> Tests

	[Fact]
	public void GetShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.Get<string>("testKey"));
	}

	[Fact]
	public void GetShouldReturnNullWhenValueNotPresent()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue("testKey", default(string)))
			.Returns(null);

		// Act
		var result = _activityContext.Get<string>("testKey");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetShouldReturnValueWhenPresent()
	{
		// Arrange
		const string expectedValue = "testValue";
		_ = A.CallTo(() => _activityContext.GetValue("testKey", default(string)))
			.Returns(expectedValue);

		// Act
		var result = _activityContext.Get<string>("testKey");

		// Assert
		result.ShouldBe(expectedValue);
	}

	[Fact]
	public void GetShouldReturnComplexTypeWhenPresent()
	{
		// Arrange
		var expectedObject = new TestClass { Name = "Test", Value = 42 };
		_ = A.CallTo(() => _activityContext.GetValue("complexKey", default(TestClass)))
			.Returns(expectedObject);

		// Act
		var result = _activityContext.Get<TestClass>("complexKey");

		// Assert
		result.ShouldBe(expectedObject);
	}

	#endregion

	#region Test Helpers

	private sealed class TestClass
	{
		public string? Name { get; init; }

		public int Value { get; init; }
	}

	#endregion
}
