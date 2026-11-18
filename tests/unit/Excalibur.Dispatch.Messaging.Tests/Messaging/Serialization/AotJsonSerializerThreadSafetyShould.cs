// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Preserves the original real concurrent workload through the selected generated JSON codec.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class AotJsonSerializerThreadSafetyShould : UnitTestBase
{
	[Fact]
	public async Task SerializeConcurrently_WithoutDataCorruption()
	{
		// Arrange
		var serializer = SystemTextJsonSerializer.Create(CoreMessageJsonContext.Default);
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
					var bytes = serializer.SerializeToBytes(input);
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

}
