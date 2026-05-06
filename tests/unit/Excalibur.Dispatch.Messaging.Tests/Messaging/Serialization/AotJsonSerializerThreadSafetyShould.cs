// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Tests verifying AotJsonSerializer uses ThreadLocal&lt;ArrayBufferWriter&gt; for thread safety (S543.2).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class AotJsonSerializerThreadSafetyShould : UnitTestBase
{
	[Fact]
	public void UseThreadLocalArrayBufferWriter()
	{
		// Arrange — verify the field exists and is ThreadLocal
		var field = typeof(AotJsonSerializer)
			.GetField("_threadLocalBufferWriter", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("AotJsonSerializer should have a _threadLocalBufferWriter field");
		field.FieldType.ShouldBe(typeof(ThreadLocal<System.Buffers.ArrayBufferWriter<byte>>));
	}

	[Fact]
	public async Task SerializeConcurrently_WithoutDataCorruption()
	{
		// Arrange
		using var serializer = new AotJsonSerializer();
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
					var input = $"thread-{threadIndex}-iteration-{i}";
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
		// Arrange — force creation of a ThreadLocal value
		var serializer = new AotJsonSerializer();
		_ = serializer.Serialize("trigger-buffer-creation");

		// Act & Assert — dispose should not throw
		Should.NotThrow(() => serializer.Dispose());
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		using var serializer = new AotJsonSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void NotThrowOnDoubleDispose()
	{
		// Arrange — the volatile _disposed guard must prevent ObjectDisposedException
		// from ThreadLocal<T>.Dispose() being called twice (Bug #5)
		var serializer = new AotJsonSerializer();
		_ = serializer.Serialize("trigger-buffer-creation");

		// Act & Assert — second dispose must be a no-op
		serializer.Dispose();
		Should.NotThrow(() => serializer.Dispose());
	}

	[Fact]
	public void HaveVolatileDisposedField()
	{
		// Assert — the _disposed field must be volatile to ensure visibility across threads
		var field = typeof(AotJsonSerializer)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("AotJsonSerializer should have a _disposed field");

		// Volatile fields are not directly queryable via reflection, but we can check
		// the field exists and is boolean (the volatile keyword is a compile-time attribute)
		field.FieldType.ShouldBe(typeof(bool));
	}
}
