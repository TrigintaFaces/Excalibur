// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

namespace Excalibur.Tests.Cdc.InMemory;

/// <summary>
/// Unit tests for <see cref="IInMemoryCdcBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCdcBuilderValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ProcessorId_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseInMemory(inmemory =>
				{
					_ = inmemory.ProcessorId(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("test-processor")]
	[InlineData("cdc-1")]
	[InlineData("inmemory")]
	public void ProcessorId_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.ProcessorId(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.ProcessorId.ShouldBe(validValue);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void BatchSize_ThrowsOnInvalidValue(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseInMemory(inmemory =>
				{
					_ = inmemory.BatchSize(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void BatchSize_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseInMemory(inmemory =>
			{
				_ = inmemory.BatchSize(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<InMemoryCdcOptions>>();
		options.Value.BatchSize.ShouldBe(validValue);
	}
}
