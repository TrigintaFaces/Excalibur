// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
}
