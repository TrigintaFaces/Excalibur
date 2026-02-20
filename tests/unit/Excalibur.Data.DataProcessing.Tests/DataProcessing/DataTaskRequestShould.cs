// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataTaskRequest"/>.
/// </summary>
[UnitTest]
public sealed class DataTaskRequestShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues_WhenCreated()
	{
		// Arrange & Act
		var request = new DataTaskRequest();

		// Assert
		request.DataTaskId.ShouldNotBe(Guid.Empty);
		request.CreatedAt.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(1));
		request.RecordType.ShouldBe(string.Empty);
		request.Attempts.ShouldBe(0);
		request.MaxAttempts.ShouldBe(0);
		request.CompletedCount.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingProperties_ViaInit()
	{
		// Arrange
		var id = Guid.NewGuid();
		var created = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		var request = new DataTaskRequest
		{
			DataTaskId = id,
			CreatedAt = created,
			RecordType = "OrderRecord",
			Attempts = 2,
			MaxAttempts = 5,
			CompletedCount = 100,
		};

		// Assert
		request.DataTaskId.ShouldBe(id);
		request.CreatedAt.ShouldBe(created);
		request.RecordType.ShouldBe("OrderRecord");
		request.Attempts.ShouldBe(2);
		request.MaxAttempts.ShouldBe(5);
		request.CompletedCount.ShouldBe(100);
	}

	[Fact]
	public void AllowMutatingAttempts()
	{
		// Arrange
		var request = new DataTaskRequest { Attempts = 0 };

		// Act
		request.Attempts++;

		// Assert
		request.Attempts.ShouldBe(1);
	}

	[Fact]
	public void GenerateUniqueDataTaskIds()
	{
		// Arrange & Act
		var request1 = new DataTaskRequest();
		var request2 = new DataTaskRequest();

		// Assert
		request1.DataTaskId.ShouldNotBe(request2.DataTaskId);
	}
}
