// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Domain.Concurrency;

namespace Excalibur.Tests;

/// <summary>
///     Unit tests for the <see cref="ActivityContextExtensions" /> class.
/// </summary>
/// <remarks>
///     Tests all extension methods for <see cref="IActivityContext" /> including parameter validation, value retrieval, and service
///     resolution functionality.
/// </remarks>
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
public class ActivityContextExtensionsShould
{
	private readonly IActivityContext _activityContext = A.Fake<IActivityContext>();

	[Fact]
	public void ApplicationNameShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ApplicationName());
	}

	[Fact]
	public void ApplicationNameShouldReturnValueFromGetValueMethod()
	{
		// Arrange
		const string expectedApplicationName = "TestApplication";
		const string defaultApplicationName = "DefaultApp";

		// Initialize ApplicationContext so ApplicationContext.ApplicationName doesn't throw
		// (The extension method evaluates the default value eagerly)
		ApplicationContext.Init(new Dictionary<string, string?> { ["ApplicationName"] = defaultApplicationName });
		try
		{
			// The extension method calls GetValue with nameof(ApplicationName) and ApplicationContext.ApplicationName as default
			_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ApplicationName), defaultApplicationName))
				.Returns(expectedApplicationName);

			// Act
			var result = _activityContext.ApplicationName();

			// Assert
			result.ShouldBe(expectedApplicationName);
		}
		finally
		{
			// Clean up static state
			ApplicationContext.Reset();
		}
	}

	[Fact]
	public void ClientAddressShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ClientAddress());
	}

	[Fact]
	public void ClientAddressShouldReturnNullWhenClientAddressNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ClientAddress), default(IClientAddress)))
			.Returns(null);

		// Act
		var result = _activityContext.ClientAddress();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ClientAddressShouldReturnValueWhenClientAddressExists()
	{
		// Arrange
		const string expectedAddress = "192.168.1.1";
		var clientAddress = A.Fake<IClientAddress>();
		_ = A.CallTo(() => clientAddress.Value).Returns(expectedAddress);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ClientAddress), default(IClientAddress)))
			.Returns(clientAddress);

		// Act
		var result = _activityContext.ClientAddress();

		// Assert
		result.ShouldBe(expectedAddress);
	}

	[Fact]
	public void ConfigurationShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.Configuration());
	}

	[Fact]
	public void ConfigurationShouldReturnConfigurationFromGetMethod()
	{
		// Arrange
		var expectedConfiguration = A.Fake<IConfiguration>();
		_ = A.CallTo(() => _activityContext.GetValue(nameof(IConfiguration), default(IConfiguration)))
			.Returns(expectedConfiguration);

		// Act
		var result = _activityContext.Configuration();

		// Assert
		result.ShouldBe(expectedConfiguration);
	}

	[Fact]
	public void CorrelationIdShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.CorrelationId());
	}

	[Fact]
	public void CorrelationIdShouldReturnEmptyGuidWhenCorrelationIdNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.CorrelationId), default(ICorrelationId)))
			.Returns(null);

		// Act
		var result = _activityContext.CorrelationId();

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void CorrelationIdShouldReturnValueWhenCorrelationIdExists()
	{
		// Arrange
		var expectedGuid = Guid.NewGuid();
		var correlationId = A.Fake<ICorrelationId>();
		_ = A.CallTo(() => correlationId.Value).Returns(expectedGuid);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.CorrelationId), default(ICorrelationId)))
			.Returns(correlationId);

		// Act
		var result = _activityContext.CorrelationId();

		// Assert
		result.ShouldBe(expectedGuid);
	}

	[Fact]
	public void ETagGetterShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ETag());
	}

	[Fact]
	public void ETagGetterShouldReturnNullWhenETagNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(null);

		// Act
		var result = _activityContext.ETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void ETagGetterShouldReturnIncomingValueWhenETagExists()
	{
		// Arrange
		const string expectedETag = """
			"abc123"
			""";
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.IncomingValue).Returns(expectedETag);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		var result = _activityContext.ETag();

		// Assert
		result.ShouldBe(expectedETag);
	}

	[Fact]
	public void ETagSetterShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;
		const string newETag = """
			"def456"
			""";

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ETag(newETag));
	}

	[Fact]
	public void ETagSetterShouldNotThrowWhenETagIsNull()
	{
		// Arrange
		const string newETag = """
			"def456"
			""";
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(null);

		// Act & Assert
		Should.NotThrow(() => _activityContext.ETag(newETag));
	}

	[Fact]
	public void ETagSetterShouldSetOutgoingValueWhenETagExists()
	{
		// Arrange
		const string newETag = """
			"def456"
			""";
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		_activityContext.ETag(newETag);

		// Assert
		_ = A.CallToSet(() => etag.OutgoingValue).To(newETag).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LatestETagShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.LatestETag());
	}

	[Fact]
	public void LatestETagShouldReturnNullWhenETagNotFound()
	{
		// Arrange
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(null);

		// Act
		var result = _activityContext.LatestETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void LatestETagShouldReturnOutgoingValueWhenNotEmpty()
	{
		// Arrange
		const string outgoingETag = """
			"outgoing123"
			""";
		const string incomingETag = """
			"incoming456"
			""";
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.OutgoingValue).Returns(outgoingETag);
		_ = A.CallTo(() => etag.IncomingValue).Returns(incomingETag);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		var result = _activityContext.LatestETag();

		// Assert
		result.ShouldBe(outgoingETag);
	}

	[Fact]
	public void LatestETagShouldReturnIncomingValueWhenOutgoingIsEmpty()
	{
		// Arrange
		const string incomingETag = """
			"incoming456"
			""";
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.OutgoingValue).Returns(string.Empty);
		_ = A.CallTo(() => etag.IncomingValue).Returns(incomingETag);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		var result = _activityContext.LatestETag();

		// Assert
		result.ShouldBe(incomingETag);
	}

	[Fact]
	public void LatestETagShouldReturnNullWhenBothValuesAreEmpty()
	{
		// Arrange
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.OutgoingValue).Returns(string.Empty);
		_ = A.CallTo(() => etag.IncomingValue).Returns(string.Empty);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		var result = _activityContext.LatestETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void LatestETagShouldReturnNullWhenIncomingValueIsNull()
	{
		// Arrange
		var etag = A.Fake<IETag>();
		_ = A.CallTo(() => etag.OutgoingValue).Returns(string.Empty);
		_ = A.CallTo(() => etag.IncomingValue).Returns(null);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.ETag), default(IETag)))
			.Returns(etag);

		// Act
		var result = _activityContext.LatestETag();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.Get<string>("testKey"));
	}

	[Fact]
	public void GetShouldCallGetValueWithCorrectParameters()
	{
		// Arrange
		const string key = "testKey";
		const string expectedValue = "testValue";
		_ = A.CallTo(() => _activityContext.GetValue(key, default(string)))
			.Returns(expectedValue);

		// Act
		var result = _activityContext.Get<string>(key);

		// Assert
		result.ShouldBe(expectedValue);
		_ = A.CallTo(() => _activityContext.GetValue(key, default(string)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ServiceProviderShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.ServiceProvider());
	}

	[Fact]
	public void ServiceProviderShouldReturnServiceProviderFromGet()
	{
		// Arrange
		var expectedServiceProvider = A.Fake<IServiceProvider>();
		_ = A.CallTo(() => _activityContext.GetValue(nameof(IServiceProvider), default(IServiceProvider)))
			.Returns(expectedServiceProvider);

		// Act
		var result = _activityContext.ServiceProvider();

		// Assert
		result.ShouldBe(expectedServiceProvider);
	}

	[Fact]
	public void DomainDbShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.DomainDb());
	}

	[Fact]
	public void DomainDbShouldReturnDomainDbFromServiceProvider()
	{
		// Arrange
		var expectedDomainDb = A.Fake<IDomainDb>();
		var serviceProvider = A.Fake<IServiceProvider>();
		// Mock GetValue for IServiceProvider key (used by ServiceProvider() extension)
		_ = A.CallTo(() => _activityContext.GetValue(nameof(IServiceProvider), A<IServiceProvider?>.Ignored))
			.Returns(serviceProvider);
		// Mock the underlying GetService method (GetRequiredService is an extension that calls GetService)
		_ = A.CallTo(() => serviceProvider.GetService(typeof(IDomainDb)))
			.Returns(expectedDomainDb);

		// Act
		var result = _activityContext.DomainDb();

		// Assert
		result.ShouldBe(expectedDomainDb);
	}

	[Fact]
	public void TenantIdShouldThrowArgumentNullExceptionWhenContextIsNull()
	{
		// Arrange
		IActivityContext? nullContext = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullContext.TenantId());
	}

	[Fact]
	public void TenantIdShouldReturnValueFromTenantIdInterface()
	{
		// Arrange
		const string expectedTenantId = "tenant123";
		var tenantId = A.Fake<ITenantId>();
		_ = A.CallTo(() => tenantId.Value).Returns(expectedTenantId);
		_ = A.CallTo(() => _activityContext.GetValue(nameof(ActivityContextExtensions.TenantId), default(ITenantId)))
			.Returns(tenantId);

		// Act
		var result = _activityContext.TenantId();

		// Assert
		result.ShouldBe(expectedTenantId);
	}
}
