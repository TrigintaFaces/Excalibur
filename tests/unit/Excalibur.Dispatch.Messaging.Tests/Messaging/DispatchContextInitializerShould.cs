// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

using Excalibur.Dispatch.Options;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatchContextInitializer"/>.
/// </summary>
/// <remarks>
/// Tests the helper methods for creating MessageContext instances.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Messaging)]
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
			_ = context.GetTraceParent().ShouldNotBeNull();
			context.GetTraceParent().ShouldBe(activity.Id);
		}
		finally
		{
			activity.Stop();
		}
	}

	/// <summary>
	/// Baggage reaches the context for the keys the application allowed, and for no others.
	/// </summary>
	/// <remarks>
	/// This arm previously asserted that EVERY baggage entry was copied. That was the defect: the runtime
	/// parses an inbound baggage header into the ambient activity without being asked, so those keys and
	/// values are caller-chosen on any publicly reachable endpoint, and copying them put caller-supplied
	/// data onto the wire and into every downstream sink. The assertion is kept rather than deleted, and
	/// strengthened: it now pins both directions of the policy in one place — the allowed key arrives, the
	/// unallowed one does not.
	/// </remarks>
	[Fact]
	public void CreateDefaultContext_WithActivityBaggage_CopiesOnlyAllowedKeysToItems()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.Configure<BaggagePropagationOptions>(o => _ = o.AllowedKeys.Add("user-id"));
		var provider = services.BuildServiceProvider();

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
			context.Items.ShouldNotContainKey("baggage.tenant-id",
				"the application allowed 'user-id' and not 'tenant-id', and an entry nobody allowed must not "
				+ "cross onto the wire or into a downstream sink");
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
	public void CreateFromHeaders_CopiesNoHeaderItWasNotAskedFor()
	{
		// This arm asserted the opposite: that EVERY supplied header landed in Items. That was the
		// defect. The method's name invites being handed a request's headers, and a context item is put
		// on the wire by every remote transport and persisted by one outbox provider -- so the caller's
		// credentials travelled with the message.
		var headers = new Dictionary<string, string?>
		{
			["X-Custom-Header"] = "custom-value",
			["Authorization"] = "Bearer a-caller-token",
			["Cookie"] = "session=a-caller-session",
		};

		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		context.Items.ShouldNotContainKey("X-Custom-Header");
		context.Items.ShouldNotContainKey("Authorization");
		context.Items.ShouldNotContainKey("Cookie");
	}

	[Fact]
	public void CreateFromHeaders_StillReadsTheContextFieldsItDefines()
	{
		// The liveness half. Every assertion above is satisfied by a factory that reads nothing at all,
		// which would silently drop correlation and tenancy -- the fields this method exists for.
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "corr-1",
			["X-Causation-ID"] = "cause-1",
			["X-Tenant-ID"] = "tenant-1",
			["Authorization"] = "Bearer a-caller-token",
		};

		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		context.CorrelationId.ShouldBe("corr-1");
		context.CausationId.ShouldBe("cause-1");
		context.GetTenantId().ShouldBe("tenant-1");
		context.Items.ShouldNotContainKey("Authorization");
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

		// Assert. The header factory no longer copies arbitrary headers; the metadata factory still
		// restores arbitrary metadata, because that metadata is what this framework itself persisted
		// rather than something a caller sent us.
		headerContext.Items.ShouldNotContainKey("X-Test");
		metadataContext.Items["TestKey"].ShouldBe("metadata-value");
	}

	#endregion
}