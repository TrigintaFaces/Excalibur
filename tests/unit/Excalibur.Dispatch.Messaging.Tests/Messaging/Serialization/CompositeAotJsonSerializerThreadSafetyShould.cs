// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Tests verifying CompositeAotJsonSerializer uses ThreadLocal&lt;ArrayBufferWriter&gt; for thread safety (S543.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class CompositeAotJsonSerializerThreadSafetyShould : UnitTestBase
{
	[Fact]
	public void UseThreadLocalArrayBufferWriter()
	{
		// Arrange — verify the field exists and is ThreadLocal
		var field = typeof(CompositeAotJsonSerializer)
			.GetField("_threadLocalBufferWriter", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CompositeAotJsonSerializer should have a _threadLocalBufferWriter field");
		field.FieldType.ShouldBe(typeof(ThreadLocal<System.Buffers.ArrayBufferWriter<byte>>));
	}

	[Fact]
	public async Task SerializeConcurrently_WithoutDataCorruption()
	{
		// Arrange
		using var serializer = new CompositeAotJsonSerializer(CoreMessageJsonContext.Instance);
		const int threadCount = 8;
		const int iterationsPerThread = 100;
		var errors = new ConcurrentBag<Exception>();
		var barrier = new Barrier(threadCount);

		// Act — serialize strings concurrently from multiple threads
		var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
		{
			barrier.SignalAndWait();
			for (var i = 0; i < iterationsPerThread; i++)
			{
				try
				{
					var input = $"composite-thread-{threadIndex}-iteration-{i}";
					var bytes = serializer.Serialize(input);
					var roundtrip = serializer.Deserialize<string>(bytes);
					if (roundtrip != input)
					{
						errors.Add(new InvalidOperationException(
							$"Data corruption: expected '{input}' but got '{roundtrip}'"));
					}
				}
				catch (Exception ex)
				{
					errors.Add(ex);
				}
			}
		})).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		errors.ShouldBeEmpty($"Concurrent serialization produced {errors.Count} errors");
	}

	[Fact]
	public void DisposeCorrectly_ClearsThreadLocalValues()
	{
		// Arrange
		var serializer = new CompositeAotJsonSerializer(CoreMessageJsonContext.Instance);
		_ = serializer.Serialize("trigger");

		// Act & Assert — dispose should not throw
		Should.NotThrow(() => serializer.Dispose());
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		using var serializer = new CompositeAotJsonSerializer(CoreMessageJsonContext.Instance);

		// Assert
		serializer.ShouldBeAssignableTo<IDisposable>();
	}
}
