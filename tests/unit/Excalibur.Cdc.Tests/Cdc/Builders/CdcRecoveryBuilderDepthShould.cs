// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.Builders;

/// <summary>
/// Depth coverage tests for CdcRecoveryBuilder validation paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcRecoveryBuilderDepthShould
{
	[Fact]
	public void SetRecoveryStrategyOnOptions()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		builder.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest);

		// Assert
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
	}

	[Fact]
	public void SetMaxAttemptsOnOptions()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		builder.MaxAttempts(5);

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(5);
	}

	[Fact]
	public void ThrowWhenMaxAttemptsIsNegative()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => builder.MaxAttempts(-1));
	}

	[Fact]
	public void AllowZeroMaxAttempts()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		builder.MaxAttempts(0);

		// Assert
		options.MaxRecoveryAttempts.ShouldBe(0);
	}

	[Fact]
	public void SetAttemptDelayOnOptions()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);
		var delay = TimeSpan.FromSeconds(30);

		// Act
		builder.AttemptDelay(delay);

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(delay);
	}

	[Fact]
	public void ThrowWhenAttemptDelayIsNegative()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			builder.AttemptDelay(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void AllowZeroAttemptDelay()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		builder.AttemptDelay(TimeSpan.Zero);

		// Assert
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ThrowWhenOnPositionResetHandlerIsNull()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.OnPositionReset(null!));
	}

	[Fact]
	public void SetOnPositionResetHandler()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);
		CdcPositionResetHandler handler = (_, _) => Task.CompletedTask;

		// Act
		builder.OnPositionReset(handler);

		// Assert
		options.OnPositionReset.ShouldBe(handler);
	}

	[Fact]
	public void EnableStructuredLogging()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		builder.EnableStructuredLogging();

		// Assert
		options.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void DisableStructuredLogging()
	{
		// Arrange
		var options = new CdcOptions { EnableStructuredLogging = true };
		var builder = CreateBuilder(options);

		// Act
		builder.EnableStructuredLogging(false);

		// Assert
		options.EnableStructuredLogging.ShouldBeFalse();
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var options = new CdcOptions();
		var builder = CreateBuilder(options);

		// Act
		var result = builder
			.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
			.MaxAttempts(3)
			.AttemptDelay(TimeSpan.FromSeconds(10))
			.EnableStructuredLogging();

		// Assert
		result.ShouldNotBeNull();
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.MaxRecoveryAttempts.ShouldBe(3);
		options.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.EnableStructuredLogging.ShouldBeTrue();
	}

	private static ICdcRecoveryBuilder CreateBuilder(CdcOptions options)
	{
		// CdcRecoveryBuilder is internal, so we use the CdcBuilder which creates it
		var services = new ServiceCollection();
		var cdcBuilder = new CdcBuilder_TestWrapper(services, options);
		ICdcRecoveryBuilder? recoveryBuilder = null;

		cdcBuilder.WithRecovery(rb => recoveryBuilder = rb);

		return recoveryBuilder!;
	}

	// Helper to access internal CdcBuilder
	private sealed class CdcBuilder_TestWrapper : ICdcBuilder
	{
		private readonly CdcOptions _options;

		public CdcBuilder_TestWrapper(IServiceCollection services, CdcOptions options)
		{
			Services = services;
			_options = options;
		}

		public IServiceCollection Services { get; }

		public ICdcBuilder TrackTable(string tableName, Action<ICdcTableBuilder> configure) => this;

		public ICdcBuilder TrackTable<TEntity>(Action<ICdcTableBuilder>? configure = null) where TEntity : class => this;

		public ICdcBuilder WithRecovery(Action<ICdcRecoveryBuilder> configure)
		{
			// Create a CdcRecoveryBuilder using reflection since it's internal
			var builderType = typeof(CdcOptions).Assembly.GetType("Excalibur.Cdc.CdcRecoveryBuilder");
			if (builderType != null)
			{
				var builder = (ICdcRecoveryBuilder)Activator.CreateInstance(builderType, _options)!;
				configure(builder);
			}

			return this;
		}

		public ICdcBuilder EnableBackgroundProcessing(bool enable = true) => this;
	}
}
