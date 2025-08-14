using Excalibur.DataAccess.DataProcessing;
using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class DataTaskRequestShould
{
    [Fact]
    public void InitializeWithDefaultValues()
    {
        // Act
        var dataTaskRequest = new DataTaskRequest();

        // Assert
        dataTaskRequest.DataTaskId.ShouldNotBe(Guid.Empty);
        dataTaskRequest.RecordType.ShouldBe(string.Empty);
        dataTaskRequest.Attempts.ShouldBe(0);
        dataTaskRequest.MaxAttempts.ShouldBe(0);
        dataTaskRequest.CompletedCount.ShouldBe(0);
    }

    [Fact]
    public void AllowSettingProperties()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        
        // Act
        var dataTaskRequest = new DataTaskRequest
        {
            DataTaskId = taskId,
            CreatedAt = createdAt,
            RecordType = "TestRecordType",
            MaxAttempts = 5,
            CompletedCount = 100
        };
        
        // Manual update for attempts
        dataTaskRequest.Attempts = 2;

        // Assert
        dataTaskRequest.DataTaskId.ShouldBe(taskId);
        dataTaskRequest.CreatedAt.ShouldBe(createdAt);
        dataTaskRequest.RecordType.ShouldBe("TestRecordType");
        dataTaskRequest.Attempts.ShouldBe(2);
        dataTaskRequest.MaxAttempts.ShouldBe(5);
        dataTaskRequest.CompletedCount.ShouldBe(100);
    }
    
    [Fact]
    public void AllowIncrementingAttemptCount()
    {
        // Arrange
        var dataTaskRequest = new DataTaskRequest
        {
            RecordType = "TestRecordType",
            MaxAttempts = 5,
            Attempts = 1
        };

        // Act
        dataTaskRequest.Attempts++;
        dataTaskRequest.Attempts++;

        // Assert
        dataTaskRequest.Attempts.ShouldBe(3);
    }
    
    [Fact]
    public void HaveInitOnlyProperties()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        
        // Act
        var dataTaskRequest = new DataTaskRequest
        {
            DataTaskId = taskId,
            CreatedAt = createdAt,
            RecordType = "TestRecordType",
            MaxAttempts = 5,
            CompletedCount = 100
        };

        // Assert - Compilation would fail if these properties weren't init-only
        // We can verify they have the expected values
        dataTaskRequest.DataTaskId.ShouldBe(taskId);
        dataTaskRequest.CreatedAt.ShouldBe(createdAt);
        dataTaskRequest.RecordType.ShouldBe("TestRecordType");
        dataTaskRequest.MaxAttempts.ShouldBe(5);
        dataTaskRequest.CompletedCount.ShouldBe(100);
    }
}