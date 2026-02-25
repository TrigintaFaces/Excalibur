// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration.Validators;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for <see cref="AzureConfigurationValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class AzureConfigurationValidatorShould : UnitTestBase
{
	private const string ValidGuid = "12345678-1234-1234-1234-123456789012";

	#region Constructor Tests

	[Fact]
	public void SetDefaultConfigurationName()
	{
		// Act
		var validator = new AzureConfigurationValidator();

		// Assert
		validator.ConfigurationName.ShouldBe("Azure:Azure");
	}

	[Fact]
	public void SetCustomConfigurationName()
	{
		// Act
		var validator = new AzureConfigurationValidator("CustomSection");

		// Assert
		validator.ConfigurationName.ShouldBe("Azure:CustomSection");
	}

	[Fact]
	public void SetPriorityTo20()
	{
		// Act
		var validator = new AzureConfigurationValidator();

		// Assert
		validator.Priority.ShouldBe(20);
	}

	#endregion

	#region Authentication Tests

	[Fact]
	public async Task ReturnSuccess_WhenTenantIdIsValidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:TenantId"] = ValidGuid
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenTenantIdIsInvalidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:TenantId"] = "not-a-guid"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Tenant ID format"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenClientIdIsValidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ClientId"] = ValidGuid,
				["Azure:ClientSecret"] = "secret"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenClientIdIsInvalidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ClientId"] = "not-a-guid"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Client ID format"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenSubscriptionIdIsValidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:SubscriptionId"] = ValidGuid
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenSubscriptionIdIsInvalidGuid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:SubscriptionId"] = "not-a-guid"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Subscription ID format"));
	}

	[Fact]
	public async Task ReturnFailure_WhenClientIdProvidedWithoutSecret()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ClientId"] = ValidGuid
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Client Secret is required"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenCompleteAuthenticationProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:TenantId"] = ValidGuid,
				["Azure:ClientId"] = ValidGuid,
				["Azure:ClientSecret"] = "my-secret",
				["Azure:SubscriptionId"] = ValidGuid
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Storage Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenStorageConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=key;EndpointSuffix=core.windows.net"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageConnectionStringMissingAccountName()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountKey=key"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing AccountName"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenStorageAccountNameIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:AccountName"] = "mystorage123",
				["Azure:Storage:AccountKey"] = "mykey"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageAccountNameIsInvalidFormat()
	{
		// Arrange - uppercase letters are invalid
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:AccountName"] = "MyStorage",
				["Azure:Storage:AccountKey"] = "mykey"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Storage account name"));
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageAccountNameTooShort()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:AccountName"] = "ab",
				["Azure:Storage:AccountKey"] = "mykey"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFailure_WhenStorageAccountNameWithoutKey()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:Storage:AccountName"] = "mystorage123"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("account key is required"));
	}

	#endregion

	#region Service Bus Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenServiceBusConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ServiceBus:ConnectionString"] = "Endpoint=sb://mynamespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mykey"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenServiceBusConnectionStringMissingEndpoint()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ServiceBus:ConnectionString"] = "SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mykey"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("missing Endpoint"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenServiceBusNamespaceIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ServiceBus:FullyQualifiedNamespace"] = "mynamespace.servicebus.windows.net"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenServiceBusNamespaceInvalidFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:ServiceBus:FullyQualifiedNamespace"] = "mynamespace.example.com"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Service Bus namespace"));
	}

	#endregion

	#region Event Hubs Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenEventHubsConnectionStringIsValid()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:EventHubs:ConnectionString"] = "Endpoint=sb://myns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=key"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenEventHubsConnectionStringMissingEndpoint()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:EventHubs:ConnectionString"] = "SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=key"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Event Hubs connection string missing Endpoint"));
	}

	[Fact]
	public async Task ReturnSuccess_WhenEventHubsNamespaceAndNameProvided()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:EventHubs:FullyQualifiedNamespace"] = "myns.servicebus.windows.net",
				["Azure:EventHubs:EventHubName"] = "my-hub"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenEventHubsNamespaceInvalidFormat()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:EventHubs:FullyQualifiedNamespace"] = "myns.example.com"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Invalid Azure Event Hubs namespace"));
	}

	[Fact]
	public async Task ReturnFailure_WhenEventHubsNamespaceWithoutName()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Azure:EventHubs:FullyQualifiedNamespace"] = "myns.servicebus.windows.net"
			})
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.Message.Contains("Event Hub name is required"));
	}

	#endregion

	#region Custom Config Section Tests

	[Fact]
	public async Task ValidateCustomConfigSection()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["CustomAzure:TenantId"] = ValidGuid
			})
			.Build();

		var validator = new AzureConfigurationValidator("CustomAzure");

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion

	#region Empty Configuration Tests

	[Fact]
	public async Task ReturnSuccess_WhenNoAzureConfigurationExists()
	{
		// Arrange - No Azure configuration at all should still be valid
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		var validator = new AzureConfigurationValidator();

		// Act
		var result = await validator.ValidateAsync(config, CancellationToken.None);

		// Assert - Should pass since no Azure configuration means no validation errors
		result.IsValid.ShouldBeTrue();
	}

	#endregion
}
