// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcRecoveryBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcRecoveryBuilderValidationShould : UnitTestBase
{
	[Theory]
	[InlineData(-1)]
	[InlineData(-10)]
	[InlineData(int.MinValue)]
	public void MaxAttempts_ThrowsOnNegative(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.WithRecovery(recovery =>
				{
					_ = recovery.MaxAttempts(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void MaxAttempts_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.MaxAttempts(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.MaxRecoveryAttempts.ShouldBe(validValue);
	}

	[Fact]
	public void AttemptDelay_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.WithRecovery(recovery =>
				{
					_ = recovery.AttemptDelay(TimeSpan.FromSeconds(-1));
				});
			}));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	public void AttemptDelay_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedDelay = TimeSpan.FromSeconds(seconds);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.AttemptDelay(expectedDelay);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryAttemptDelay.ShouldBe(expectedDelay);
	}

	[Fact]
	public void OnPositionReset_ThrowsOnNullHandler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.WithRecovery(recovery =>
				{
					_ = recovery.OnPositionReset(null!);
				});
			}));
	}

	[Fact]
	public void OnPositionReset_SetsHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		CdcPositionResetHandler handler = (_, _) => Task.CompletedTask;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.OnPositionReset(handler);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		_ = options.Value.OnPositionReset.ShouldNotBeNull();
	}

	[Fact]
	public void InvokeCallbackStrategy_ThrowsWhenHandlerNotSet()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		// The validation happens during options build
		var ex = Should.Throw<InvalidOperationException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.WithRecovery(recovery =>
				{
					_ = recovery.Strategy(StalePositionRecoveryStrategy.InvokeCallback);
					// Note: Not setting OnPositionReset
				});
			}));

		ex.Message.ShouldContain("OnPositionReset");
		ex.Message.ShouldContain("InvokeCallback");
	}

	[Fact]
	public void InvokeCallbackStrategy_SucceedsWhenHandlerSet()
	{
		// Arrange
		var services = new ServiceCollection();
		CdcPositionResetHandler handler = (args, ct) => Task.CompletedTask;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.Strategy(StalePositionRecoveryStrategy.InvokeCallback)
						.OnPositionReset(handler);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.InvokeCallback);
		_ = options.Value.OnPositionReset.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(StalePositionRecoveryStrategy.Throw)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToEarliest)]
	[InlineData(StalePositionRecoveryStrategy.FallbackToLatest)]
	public void OtherStrategies_WorkWithoutHandler(StalePositionRecoveryStrategy strategy)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.Strategy(strategy);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryStrategy.ShouldBe(strategy);
	}
}
