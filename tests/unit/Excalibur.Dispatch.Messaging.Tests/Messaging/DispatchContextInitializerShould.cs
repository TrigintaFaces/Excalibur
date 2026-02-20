// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatchContextInitializer"/>.
/// </summary>
/// <remarks>
/// Tests the helper methods for creating MessageContext instances.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class DispatchContextInitializerShould
{
	#region CreateDefaultContext() Tests

	[Fact]
	public void CreateDefaultContext_ReturnsMessageContext()
	{
		// Arrange & Act
		var context = DispatchContextInitializer.CreateDefaultContext();

		// Assert
		_ = context.ShouldNotBeNull();
	}

	[Fact]
	public void CreateDefaultContext_SetsCorrelationId()
	{
		// Arrange & Act
		var context = DispatchContextInitializer.CreateDefaultContext();

		// Assert
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateDefaultContext_HasValidServiceProvider()
	{
		// Arrange & Act
		var context = DispatchContextInitializer.CreateDefaultContext();

		// Assert
		_ = context.RequestServices.ShouldNotBeNull();
	}

	#endregion

	#region CreateDefaultContext(IServiceProvider) Tests

	[Fact]
	public void CreateDefaultContext_WithServiceProvider_UsesProvidedProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton("test-service");
		var provider = services.BuildServiceProvider();

		// Act
		var context = DispatchContextInitializer.CreateDefaultContext(provider);

		// Assert
		context.RequestServices.ShouldBe(provider);
	}

	[Fact]
	public void CreateDefaultContext_WithServiceProvider_SetsCorrelationId()
	{
		// Arrange
		var provider = new ServiceCollection().BuildServiceProvider();

		// Act
		var context = DispatchContextInitializer.CreateDefaultContext(provider);

		// Assert
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateDefaultContext_WithCurrentActivity_UsesActivityTraceId()
	{
		// Arrange
		var provider = new ServiceCollection().BuildServiceProvider();
		using var activity = new Activity("TestOperation");
		_ = activity.Start();

		try
		{
			// Act
			var context = DispatchContextInitializer.CreateDefaultContext(provider);

			// Assert
			context.CorrelationId.ShouldBe(activity.TraceId.ToString());
		}
		finally
		{
			activity.Stop();
		}
	}

	[Fact]
	public void CreateDefaultContext_WithCurrentActivity_SetsTraceParent()
	{
		// Arrange
		var provider = new ServiceCollection().BuildServiceProvider();
		using var activity = new Activity("TestOperation");
		_ = activity.Start();

		try
		{
			// Act
			var context = DispatchContextInitializer.CreateDefaultContext(provider);

			// Assert
			_ = context.TraceParent.ShouldNotBeNull();
			context.TraceParent.ShouldBe(activity.Id);
		}
		finally
		{
			activity.Stop();
		}
	}

	[Fact]
	public void CreateDefaultContext_WithActivityBaggage_CopiesBaggageToItems()
	{
		// Arrange
		var provider = new ServiceCollection().BuildServiceProvider();
		using var activity = new Activity("TestOperation");
		_ = activity.AddBaggage("user-id", "user-123");
		_ = activity.AddBaggage("tenant-id", "tenant-456");
		_ = activity.Start();

		try
		{
			// Act
			var context = DispatchContextInitializer.CreateDefaultContext(provider);

			// Assert
			context.Items.ShouldContainKey("baggage.user-id");
			context.Items["baggage.user-id"].ShouldBe("user-123");
			context.Items.ShouldContainKey("baggage.tenant-id");
			context.Items["baggage.tenant-id"].ShouldBe("tenant-456");
		}
		finally
		{
			activity.Stop();
		}
	}

	[Fact]
	public void CreateDefaultContext_WithNoActivity_GeneratesNewCorrelationId()
	{
		// Arrange
		var provider = new ServiceCollection().BuildServiceProvider();
		Activity.Current = null;

		// Act
		var context = DispatchContextInitializer.CreateDefaultContext(provider);

		// Assert
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
		Guid.TryParse(context.CorrelationId, out _).ShouldBeTrue();
	}

	#endregion

	#region CreateFromHeaders Tests

	[Fact]
	public void CreateFromHeaders_WithNullHeaders_ThrowsArgumentNullException()
	{
		// Arrange
		IDictionary<string, string?> headers = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => DispatchContextInitializer.CreateFromHeaders(headers));
	}

	[Fact]
	public void CreateFromHeaders_WithEmptyHeaders_ReturnsContext()
	{
		// Arrange
		var headers = new Dictionary<string, string?>();

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		_ = context.ShouldNotBeNull();
	}

	[Fact]
	public void CreateFromHeaders_WithCorrelationId_SetsCorrelationId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "correlation-12345",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CorrelationId.ShouldBe("correlation-12345");
	}

	[Fact]
	public void CreateFromHeaders_WithCausationId_SetsCausationId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Causation-ID"] = "causation-67890",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CausationId.ShouldBe("causation-67890");
	}

	[Fact]
	public void CreateFromHeaders_WithWhitespaceCorrelationId_UsesDefault()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "   ",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CorrelationId.ShouldNotBe("   ");
	}

	[Fact]
	public void CreateFromHeaders_CopiesAllHeadersToItems()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Custom-Header"] = "custom-value",
			["X-Another-Header"] = "another-value",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.Items.ShouldContainKey("X-Custom-Header");
		context.Items["X-Custom-Header"].ShouldBe("custom-value");
		context.Items.ShouldContainKey("X-Another-Header");
		context.Items["X-Another-Header"].ShouldBe("another-value");
	}

	[Fact]
	public void CreateFromHeaders_WithNullHeaderValue_UsesEmptyString()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Null-Header"] = null,
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.Items["X-Null-Header"].ShouldBe(string.Empty);
	}

	#endregion

	#region CreateFromMetadata Tests

	[Fact]
	public void CreateFromMetadata_WithNullMetadata_ThrowsArgumentNullException()
	{
		// Arrange
		IDictionary<string, string?> metadata = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => DispatchContextInitializer.CreateFromMetadata(metadata));
	}

	[Fact]
	public void CreateFromMetadata_WithEmptyMetadata_ReturnsContext()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>();

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		_ = context.ShouldNotBeNull();
	}

	[Fact]
	public void CreateFromMetadata_WithCorrelationId_SetsCorrelationId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CorrelationId"] = "meta-correlation-123",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CorrelationId.ShouldBe("meta-correlation-123");
	}

	[Fact]
	public void CreateFromMetadata_WithCausationId_SetsCausationId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CausationId"] = "meta-causation-456",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CausationId.ShouldBe("meta-causation-456");
	}

	[Fact]
	public void CreateFromMetadata_WithWhitespaceCorrelationId_UsesDefault()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CorrelationId"] = "  ",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CorrelationId.ShouldNotBe("  ");
	}

	[Fact]
	public void CreateFromMetadata_CopiesAllMetadataToItems()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CustomKey"] = "custom-value",
			["AnotherKey"] = "another-value",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.Items.ShouldContainKey("CustomKey");
		context.Items["CustomKey"].ShouldBe("custom-value");
		context.Items.ShouldContainKey("AnotherKey");
		context.Items["AnotherKey"].ShouldBe("another-value");
	}

	[Fact]
	public void CreateFromMetadata_WithNullMetadataValue_UsesEmptyString()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["NullKey"] = null,
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.Items["NullKey"].ShouldBe(string.Empty);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public void CreateFromHeaders_ThenCreateFromMetadata_BothWork()
	{
		// Arrange
		var headers = new Dictionary<string, string?> { ["X-Test"] = "header-value" };
		var metadata = new Dictionary<string, string?> { ["TestKey"] = "metadata-value" };

		// Act
		var headerContext = DispatchContextInitializer.CreateFromHeaders(headers);
		var metadataContext = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		headerContext.Items["X-Test"].ShouldBe("header-value");
		metadataContext.Items["TestKey"].ShouldBe("metadata-value");
	}

	#endregion
}
