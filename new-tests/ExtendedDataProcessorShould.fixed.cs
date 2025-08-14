using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.DataAccess;
using Excalibur.DataAccess.DataProcessing;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class ExtendedDataProcessorShould
{
    private readonly TestDataProcessor _dataProcessor;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IOptions<DataProcessingConfiguration> _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestDataProcessor> _logger;
    private readonly IRecordHandler<string> _recordHandler;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    private readonly IServiceScope _serviceScope;

    public ExtendedDataProcessorShould()
    {
        _appLifetime = A.Fake<IHostApplicationLifetime>();
        
        var config = new DataProcessingConfiguration
        {
            QueueSize = 100,
            ProducerBatchSize = 10,
            ConsumerBatchSize = 5
        };
        _configuration = Options.Create(config);
        
        _recordHandler = A.Fake<IRecordHandler<string>>();
        _serviceScope = A.Fake<IServiceScope>();
        _serviceProvider = A.Fake<IServiceProvider>();
        var scopedServiceProvider = A.Fake<IServiceProvider>();
        
        A.CallTo(() => _serviceProvider.CreateScope()).Returns(_serviceScope);
        A.CallTo(() => _serviceScope.ServiceProvider).Returns(scopedServiceProvider);
        A.CallTo(() => scopedServiceProvider.GetRequiredService<IRecordHandler<string>>())
            .Returns(_recordHandler);
        
        _logger = A.Fake<ILogger<TestDataProcessor>>();
        
        _dataProcessor = new TestDataProcessor(
            _appLifetime, 
            _configuration, 
            _serviceProvider, 
            _logger);
    }

    [Fact]
    public void ThrowObjectDisposedExceptionWhenRunningDisposedInstance()
    {
        // Arrange
        _dataProcessor.Dispose();
        
        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => 
            _dataProcessor.RunAsync(0, async (count, _) => { await Task.CompletedTask; }, _cancellationToken));
    }
    
    [Fact]
    public async Task ProcessAllRecordsInBatch()
    {
        // Arrange
        var testData = new List<string> { "test1", "test2", "test3" };
        _dataProcessor.SetTestData(testData);
        
        var updatedCompleteCount = 0L;
        var updateCompletedHandler = new UpdateCompletedCount((count, _) =>
        {
            updatedCompleteCount = count;
            return Task.CompletedTask;
        });
        
        // Act
        var result = await _dataProcessor.RunAsync(0, updateCompletedHandler, _cancellationToken).ConfigureAwait(true);
        
        // Assert
        result.ShouldBe(3); // We processed 3 records
        updatedCompleteCount.ShouldBe(3); // Last update should be 3
        
        // Verify each record was processed
        foreach (var item in testData)
        {
            A.CallTo(() => _recordHandler.HandleAsync(item, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }
    }
    
    [Fact]
    public async Task ResumeProcessingFromCompletedCount()
    {
        // Arrange
        var testData = new List<string> { "test1", "test2", "test3" };
        _dataProcessor.SetTestData(testData);
        
        var updatedCompleteCount = 0L;
        var updateCompletedHandler = new UpdateCompletedCount((count, _) =>
        {
            updatedCompleteCount = count;
            return Task.CompletedTask;
        });
        
        // Act - Start with 2 already completed
        var result = await _dataProcessor.RunAsync(2, updateCompletedHandler, _cancellationToken).ConfigureAwait(true);
        
        // Assert
        result.ShouldBe(3); // We processed 1 more record (on top of the 2 already "completed")
        updatedCompleteCount.ShouldBe(3); // Last update should be 3
        
        // Only the last item should have been processed
        A.CallTo(() => _recordHandler.HandleAsync("test1", A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _recordHandler.HandleAsync("test2", A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _recordHandler.HandleAsync("test3", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StopProcessingWhenCancellationRequested()
    {
        // Arrange
        var testData = new List<string> { "test1", "test2", "test3", "test4", "test5" };
        _dataProcessor.SetTestData(testData);
        
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        
        // Setup the record handler to cancel after processing the first item
        A.CallTo(() => _recordHandler.HandleAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => 
            {
                callCount++;
                if (callCount == 1)
                {
                    cts.Cancel();
                }
            });
            
        // Act
        await Should.ThrowAsync<OperationCanceledException>(async () => 
            await _dataProcessor.RunAsync(0, (_, _) => Task.CompletedTask, cts.Token).ConfigureAwait(true)
        ).ConfigureAwait(true);
        
        // Assert
        callCount.ShouldBe(1); // Only one record should have been processed
    }
    
    [Fact]
    public async Task HandleExceptionsInRecordProcessing()
    {
        // Arrange
        var testData = new List<string> { "test1", "test2", "test3" };
        _dataProcessor.SetTestData(testData);
        
        // Setup the second record to throw an exception
        A.CallTo(() => _recordHandler.HandleAsync("test2", A<CancellationToken>._))
            .Throws(new InvalidOperationException("Test exception"));
        
        var updatedCompleteCount = 0L;
        var updateCompletedHandler = new UpdateCompletedCount((count, _) =>
        {
            updatedCompleteCount = count;
            return Task.CompletedTask;
        });
        
        // Act - We should complete without exceptions even though one record fails
        var result = await _dataProcessor.RunAsync(0, updateCompletedHandler, _cancellationToken).ConfigureAwait(true);
        
        // Assert
        // We still processed all records (even though one failed)
        result.ShouldBe(3);
        // But only incremented the successful ones
        updatedCompleteCount.ShouldBe(2);
        
        A.CallTo(() => _recordHandler.HandleAsync("test1", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _recordHandler.HandleAsync("test2", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _recordHandler.HandleAsync("test3", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task DisposeResourcesWhenApplicationIsStopping()
    {
        // Arrange
        var cancellationToken = A.Fake<CancellationToken>();
        var callback = A.Fake<Action>();
        
        A.CallTo(() => _appLifetime.ApplicationStopping).Returns(new CancellationToken());
        
        // Ensure we can access the callback registered with the application lifetime
        A.CallTo(() => _appLifetime.ApplicationStopping.Register(A<Action>._))
            .Invokes((Action action) => callback = action);
        
        // Act
        // Trigger the application stopping callback
        callback();
        
        // Allow some time for async operations to complete
        await Task.Delay(100).ConfigureAwait(true);
        
        // Assert
        // Since we're checking internal state indirectly, we can verify the producer is marked as stopped
        _dataProcessor.IsProducerStopped.ShouldBeTrue();
    }
    
    public class TestDataProcessor : DataProcessor<string>
    {
        private List<string> _testData = new();
        
        public TestDataProcessor(
            IHostApplicationLifetime appLifetime, 
            IOptions<DataProcessingConfiguration> configuration, 
            IServiceProvider serviceProvider, 
            ILogger logger) 
            : base(appLifetime, configuration, serviceProvider, logger)
        {
        }

        public void SetTestData(List<string> testData)
        {
            _testData = testData;
        }
        
        public bool IsProducerStopped => GetProducerStopped();

        public override Task<IEnumerable<string>> FetchBatchAsync(long skip, int batchSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var result = _testData
                .Skip((int)skip)
                .Take(batchSize)
                .ToList();
                
            return Task.FromResult<IEnumerable<string>>(result);
        }
        
        // Helper method to expose private field for testing
        private bool GetProducerStopped()
        {
            // Access to _producerStopped field through reflection
            var fieldInfo = GetType().BaseType?.GetField("_producerStopped", 
                BindingFlags.NonPublic | 
                BindingFlags.Instance);
                
            return (bool)(fieldInfo?.GetValue(this) ?? false);
        }
    }
}