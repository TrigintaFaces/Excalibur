// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Pooling;

namespace Excalibur.Dispatch.Tests.Options.Pooling;

/// <summary>
/// Unit tests for <see cref="PoolOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class PoolOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_BufferPool_IsNotNull()
	{
		// Arrange & Act
		var options = new PoolOptions();

		// Assert
		_ = options.BufferPool.ShouldNotBeNull();
	}

	[Fact]
	public void Default_MessagePool_IsNotNull()
	{
		// Arrange & Act
		var options = new PoolOptions();

		// Assert
		_ = options.MessagePool.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Global_IsNotNull()
	{
		// Arrange & Act
		var options = new PoolOptions();

		// Assert
		_ = options.Global.ShouldNotBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void BufferPool_CanBeSet()
	{
		// Arrange
		var options = new PoolOptions();
		var bufferPoolOptions = new BufferPoolOptions();

		// Act
		options.BufferPool = bufferPoolOptions;

		// Assert
		options.BufferPool.ShouldBe(bufferPoolOptions);
	}

	[Fact]
	public void MessagePool_CanBeSet()
	{
		// Arrange
		var options = new PoolOptions();
		var messagePoolOptions = new MessagePoolOptions();

		// Act
		options.MessagePool = messagePoolOptions;

		// Assert
		options.MessagePool.ShouldBe(messagePoolOptions);
	}

	[Fact]
	public void Global_CanBeSet()
	{
		// Arrange
		var options = new PoolOptions();
		var globalOptions = new GlobalPoolOptions();

		// Act
		options.Global = globalOptions;

		// Assert
		options.Global.ShouldBe(globalOptions);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var bufferPoolOptions = new BufferPoolOptions();
		var messagePoolOptions = new MessagePoolOptions();
		var globalOptions = new GlobalPoolOptions();

		// Act
		var options = new PoolOptions
		{
			BufferPool = bufferPoolOptions,
			MessagePool = messagePoolOptions,
			Global = globalOptions,
		};

		// Assert
		options.BufferPool.ShouldBe(bufferPoolOptions);
		options.MessagePool.ShouldBe(messagePoolOptions);
		options.Global.ShouldBe(globalOptions);
	}

	#endregion

	#region Default Instances Are Distinct

	[Fact]
	public void Default_BufferPool_IsDistinctPerInstance()
	{
		// Arrange
		var options1 = new PoolOptions();
		var options2 = new PoolOptions();

		// Assert - Each instance should have its own BufferPool
		options1.BufferPool.ShouldNotBeSameAs(options2.BufferPool);
	}

	[Fact]
	public void Default_MessagePool_IsDistinctPerInstance()
	{
		// Arrange
		var options1 = new PoolOptions();
		var options2 = new PoolOptions();

		// Assert - Each instance should have its own MessagePool
		options1.MessagePool.ShouldNotBeSameAs(options2.MessagePool);
	}

	[Fact]
	public void Default_Global_IsDistinctPerInstance()
	{
		// Arrange
		var options1 = new PoolOptions();
		var options2 = new PoolOptions();

		// Assert - Each instance should have its own Global
		options1.Global.ShouldNotBeSameAs(options2.Global);
	}

	#endregion
}
