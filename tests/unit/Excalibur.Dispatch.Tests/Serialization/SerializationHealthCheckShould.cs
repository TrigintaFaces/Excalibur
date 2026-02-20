// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializationHealthCheck"/> validating health check behavior
/// for various serialization configurations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerializationHealthCheckShould
{
	private readonly ILogger<SerializationHealthCheck> _logger = NullLogger<SerializationHealthCheck>.Instance;

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SerializationHealthCheck(null!, _logger))
			.ParamName.ShouldBe("registry");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var registry = new SerializerRegistry();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new SerializationHealthCheck(registry, null!))
			.ParamName.ShouldBe("logger");
	}

	#endregion Constructor Tests

	#region Unhealthy Tests

	[Fact]
	public async Task ReturnUnhealthy_WhenNoCurrentSerializerConfigured()
	{
		// Arrange
		var registry = new SerializerRegistry();
		// Note: No serializer registered or set as current

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("No current serializer configured");
	}

	#endregion Unhealthy Tests

	#region Healthy Tests

	[Fact]
	public async Task ReturnHealthy_WhenSerializerConfiguredAndWorks()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.SetCurrent("MemoryPack");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		// Accept Healthy or Degraded — under thread pool starvation (full suite runs),
		// JIT warmup or GC pauses can push round-trip above the 50ms degradation threshold.
		result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
		result.Data.ShouldContainKey("current_serializer");
		result.Data.ShouldContainKey("total_registered");
		result.Data["total_registered"].ShouldBe(1);
		result.Data.ShouldContainKey("verified_count");
		result.Data["verified_count"].ShouldBe(1);
	}

	[Fact]
	public async Task ReturnHealthy_WithMultipleSerializers()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		var jsonSerializer = new SystemTextJsonPluggableSerializer();

		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.Register(SerializerIds.SystemTextJson, jsonSerializer);
		registry.SetCurrent("MemoryPack");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		// Accept Healthy or Degraded — under thread pool starvation (full suite runs with 7,000+ tests),
		// JIT warmup or GC pauses can push round-trip above the 50ms degradation threshold.
		// Both statuses confirm serializers are functional; Degraded only indicates slowness.
		result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded);
		result.Data["total_registered"].ShouldBe(2);
		result.Data["verified_count"].ShouldBe(2);
		result.Data.ShouldContainKey("serializer_MemoryPack");
		result.Data.ShouldContainKey("serializer_System.Text.Json");
	}

	#endregion Healthy Tests

	#region Degraded Tests

	[Fact]
	public async Task ReturnDegraded_WhenSerializerFailsRoundTrip()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var failingSerializer = new FailingSerializeSerializer();
		registry.Register(SerializerIds.CustomRangeStart, failingSerializer);
		registry.SetCurrent("FailingSerialize");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("failed");
		result.Data["failed_count"].ShouldBe(1);
	}

	[Fact]
	public async Task ReturnDegraded_WhenSerializerThrowsDuringDeserialization()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var failingSerializer = new FailingDeserializeSerializer();
		registry.Register(SerializerIds.CustomRangeStart, failingSerializer);
		registry.SetCurrent("FailingDeserialize");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("failed");
	}

	/// <summary>
	/// Test serializer that fails during serialization.
	/// </summary>
	private sealed class FailingSerializeSerializer : IPluggableSerializer
	{
		public string Name => "FailingSerialize";
		public string Version => "1.0.0";

		public byte[] Serialize<T>(T value)
			=> throw new InvalidOperationException("Intentional serialization failure");

		public T Deserialize<T>(ReadOnlySpan<byte> data)
			=> throw new InvalidOperationException("Should not be called");

		public byte[] SerializeObject(object value, Type type)
			=> throw new InvalidOperationException("Intentional serialization failure");

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
			=> throw new InvalidOperationException("Should not be called");
	}

	/// <summary>
	/// Test serializer that fails during deserialization.
	/// </summary>
	private sealed class FailingDeserializeSerializer : IPluggableSerializer
	{
		public string Name => "FailingDeserialize";
		public string Version => "1.0.0";

		public byte[] Serialize<T>(T value) => [1, 2, 3];

		public T Deserialize<T>(ReadOnlySpan<byte> data)
			=> throw new InvalidOperationException("Intentional deserialization failure");

		public byte[] SerializeObject(object value, Type type) => [1, 2, 3];

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
			=> throw new InvalidOperationException("Intentional deserialization failure");
	}

	#endregion Degraded Tests

	#region Cancellation Tests

	[Fact]
	public async Task RespectCancellationToken_WhenCancelled()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.SetCurrent("MemoryPack");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert - the method throws synchronously but still returns a Task,
		// so we need to catch the exception from the task execution
		var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await healthCheck.CheckHealthAsync(context, cts.Token));

		_ = exception.ShouldNotBeNull();
	}

	#endregion Cancellation Tests

	#region Data Reporting Tests

	[Fact]
	public async Task ReportSerializerStatusInData()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.SetCurrent("MemoryPack");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		var serializerStatus = result.Data["serializer_MemoryPack"] as string;
		_ = serializerStatus.ShouldNotBeNull();
		// Accept either "Passed" or "Slow" - both indicate successful verification
		// "Slow" just means the serialization exceeded the 50ms threshold (common on loaded machines)
		(serializerStatus.Contains("Passed") || serializerStatus.Contains("Slow")).ShouldBeTrue(
			$"Expected status to contain 'Passed' or 'Slow', but was: {serializerStatus}");
		serializerStatus.ShouldContain("ms"); // Should include timing
	}

	[Fact]
	public async Task ReportCurrentSerializerInfo()
	{
		// Arrange
		var registry = new SerializerRegistry();
		var memoryPackSerializer = new MemoryPackPluggableSerializer();
		registry.Register(SerializerIds.MemoryPack, memoryPackSerializer);
		registry.SetCurrent("MemoryPack");

		var healthCheck = new SerializationHealthCheck(registry, _logger);
		var context = CreateHealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		var currentSerializer = result.Data["current_serializer"] as string;
		_ = currentSerializer.ShouldNotBeNull();
		currentSerializer.ShouldContain("MemoryPack");
		currentSerializer.ShouldContain("0x01");
	}

	#endregion Data Reporting Tests

	#region Helper Methods

	private static HealthCheckContext CreateHealthCheckContext()
	{
		return new HealthCheckContext
		{
			Registration = new HealthCheckRegistration(
				"serialization",
				_ => throw new NotImplementedException(),
				null,
				null)
		};
	}

	#endregion Helper Methods
}
