// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchContextInitializerShould
{
	[Fact]
	public void CreateDefaultContext_ReturnValidContext()
	{
		// Act
		var context = DispatchContextInitializer.CreateDefaultContext();

		// Assert
		context.ShouldNotBeNull();
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateDefaultContext_WithServiceProvider_ReturnValidContext()
	{
		// Arrange
		using var provider = new ServiceCollection().BuildServiceProvider();

		// Act
		var context = DispatchContextInitializer.CreateDefaultContext(provider);

		// Assert
		context.ShouldNotBeNull();
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateFromHeaders_WithCorrelationId_SetCorrelationId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "header-corr-123",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CorrelationId.ShouldBe("header-corr-123");
	}

	[Fact]
	public void CreateFromHeaders_WithCausationId_SetCausationId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Causation-ID"] = "header-caus-456",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CausationId.ShouldBe("header-caus-456");
	}

	[Fact]
	public void CreateFromHeaders_StoreAllHeadersInItems()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Custom"] = "custom-value",
			["X-Another"] = null,
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.Items["X-Custom"].ShouldBe("custom-value");
		context.Items["X-Another"].ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateFromHeaders_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => DispatchContextInitializer.CreateFromHeaders(null!));
	}

	[Fact]
	public void CreateFromHeaders_WithEmptyCorrelationId_IgnoreEmpty()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert - should keep default correlation id, not set to empty
		context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateFromMetadata_WithCorrelationId_SetCorrelationId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CorrelationId"] = "meta-corr-789",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CorrelationId.ShouldBe("meta-corr-789");
	}

	[Fact]
	public void CreateFromMetadata_WithCausationId_SetCausationId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["CausationId"] = "meta-caus-101",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CausationId.ShouldBe("meta-caus-101");
	}

	[Fact]
	public void CreateFromMetadata_StoreAllMetadataInItems()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["SomeKey"] = "some-value",
			["NullKey"] = null,
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.Items["SomeKey"].ShouldBe("some-value");
		context.Items["NullKey"].ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateFromMetadata_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => DispatchContextInitializer.CreateFromMetadata(null!));
	}

	#region T.1 Regression -- TraceParent extraction (Sprint 690)

	[Fact]
	public void CreateFromMetadata_WithTraceParent_SetTraceParent()
	{
		// Arrange
		var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
		var metadata = new Dictionary<string, string?>
		{
			["TraceParent"] = traceParent,
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().TraceParent.ShouldBe(traceParent);
	}

	[Fact]
	public void CreateFromMetadata_WithEmptyTraceParent_IgnoreEmpty()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["TraceParent"] = "",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert -- empty should not overwrite default TraceParent
		// The default context sets TraceParent from Activity.Current?.Id
		// With no Activity, it may be null, but the empty value should not be set
		var identity = context.GetOrCreateIdentityFeature();
		identity.TraceParent.ShouldNotBe("");
	}

	[Fact]
	public void CreateFromMetadata_WithWhitespaceTraceParent_IgnoreWhitespace()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["TraceParent"] = "   ",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().TraceParent.ShouldNotBe("   ");
	}

	[Fact]
	public void CreateFromHeaders_WithUserId_SetUserId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-User-ID"] = "user-42",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.GetOrCreateIdentityFeature().UserId.ShouldBe("user-42");
	}

	[Fact]
	public void CreateFromHeaders_WithTenantId_SetTenantId()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Tenant-ID"] = "tenant-abc",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.GetOrCreateIdentityFeature().TenantId.ShouldBe("tenant-abc");
	}

	#endregion

	#region T.2 Regression -- TenantId/UserId extraction from metadata (Sprint 690)

	[Fact]
	public void CreateFromMetadata_WithUserId_SetUserId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["UserId"] = "user-99",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().UserId.ShouldBe("user-99");
	}

	[Fact]
	public void CreateFromMetadata_WithTenantId_SetTenantId()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["TenantId"] = "tenant-xyz",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().TenantId.ShouldBe("tenant-xyz");
	}

	[Fact]
	public void CreateFromMetadata_WithEmptyUserId_IgnoreEmpty()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["UserId"] = "",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().UserId.ShouldBeNull();
	}

	[Fact]
	public void CreateFromMetadata_WithEmptyTenantId_IgnoreEmpty()
	{
		// Arrange
		var metadata = new Dictionary<string, string?>
		{
			["TenantId"] = "  ",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.GetOrCreateIdentityFeature().TenantId.ShouldBeNull();
	}

	[Fact]
	public void CreateFromMetadata_WithAllIdentityFields_SetAll()
	{
		// Arrange -- full round-trip scenario
		var metadata = new Dictionary<string, string?>
		{
			["CorrelationId"] = "corr-001",
			["CausationId"] = "cause-002",
			["TraceParent"] = "00-trace-span-flags",
			["UserId"] = "user-admin",
			["TenantId"] = "tenant-acme",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromMetadata(metadata);

		// Assert
		context.CorrelationId.ShouldBe("corr-001");
		context.CausationId.ShouldBe("cause-002");
		var identity = context.GetOrCreateIdentityFeature();
		identity.TraceParent.ShouldBe("00-trace-span-flags");
		identity.UserId.ShouldBe("user-admin");
		identity.TenantId.ShouldBe("tenant-acme");
	}

	[Fact]
	public void CreateFromHeaders_WithEmptyUserId_IgnoreEmpty()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-User-ID"] = "",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.GetOrCreateIdentityFeature().UserId.ShouldBeNull();
	}

	[Fact]
	public void CreateFromHeaders_WithAllIdentityFields_SetAll()
	{
		// Arrange
		var headers = new Dictionary<string, string?>
		{
			["X-Correlation-ID"] = "hdr-corr",
			["X-Causation-ID"] = "hdr-cause",
			["X-User-ID"] = "hdr-user",
			["X-Tenant-ID"] = "hdr-tenant",
		};

		// Act
		var context = DispatchContextInitializer.CreateFromHeaders(headers);

		// Assert
		context.CorrelationId.ShouldBe("hdr-corr");
		context.CausationId.ShouldBe("hdr-cause");
		var identity = context.GetOrCreateIdentityFeature();
		identity.UserId.ShouldBe("hdr-user");
		identity.TenantId.ShouldBe("hdr-tenant");
	}

	#endregion
}
