// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Redis;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderRedisExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 781: Rewired from old Action&lt;RedisOutboxOptions&gt; to Action&lt;IRedisOutboxBuilder&gt;.
/// Tests verify the new builder-based entry point.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Redis")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class RedisOutboxExtensionsShould
{
	#region UseRedis with Builder Tests

	[Fact]
	public void UseRedis_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		Excalibur.Outbox.IOutboxBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder!.UseRedis(redis => redis.ConnectionString("localhost:6379")));
	}

	[Fact]
	public void UseRedis_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Excalibur.Outbox.IOutboxBuilder builder = A.Fake<Excalibur.Outbox.IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseRedis((Action<IRedisOutboxBuilder>)null!));
	}

	[Fact]
	public void UseRedis_ReturnsBuilder_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		Excalibur.Outbox.IOutboxBuilder builder = A.Fake<Excalibur.Outbox.IOutboxBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.UseRedis(redis => redis.ConnectionString("localhost:6379"));

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void UseRedis_RegistersRedisOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseRedis(redis => redis.ConnectionString("localhost:6379")));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(RedisOutboxStore));
	}

	[Fact]
	public void UseRedis_RegistersKeyedIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseRedis(redis => redis.ConnectionString("localhost:6379")));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IOutboxStore) && sd.IsKeyedService);
	}

	[Fact]
	public void UseRedis_ConfigureIsInvoked()
	{
		// Arrange
		var services = new ServiceCollection();
		var configured = false;

		// Act
		services.AddExcaliburOutbox(outbox =>
			outbox.UseRedis(redis =>
			{
				redis.ConnectionString("localhost:6379");
				configured = true;
			}));

		// Assert
		configured.ShouldBeTrue();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(OutboxBuilderRedisExtensions).IsAbstract.ShouldBeTrue();
		typeof(OutboxBuilderRedisExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(OutboxBuilderRedisExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
