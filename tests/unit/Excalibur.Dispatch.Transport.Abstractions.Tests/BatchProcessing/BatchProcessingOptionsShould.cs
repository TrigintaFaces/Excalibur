using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class BatchProcessingOptionsShould
{
    [Fact]
    public void Should_Default_MaxBatchSize_To_100()
    {
        var options = new BatchProcessingOptions();

        options.MaxBatchSize.ShouldBe(100);
    }

    [Fact]
    public void Should_Default_BatchTimeout_To_30_Seconds()
    {
        var options = new BatchProcessingOptions();

        options.BatchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Should_Default_ProcessInParallel_To_True()
    {
        var options = new BatchProcessingOptions();

        options.ProcessInParallel.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_MaxDegreeOfParallelism_To_ProcessorCount()
    {
        var options = new BatchProcessingOptions();

        options.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
    }

    [Fact]
    public void Should_Default_ContinueOnError_To_True()
    {
        var options = new BatchProcessingOptions();

        options.ContinueOnError.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_RetryPolicy_To_New_Instance()
    {
        var options = new BatchProcessingOptions();

        options.RetryPolicy.ShouldNotBeNull();
    }

    [Fact]
    public void Should_Default_EnableMetrics_To_True()
    {
        var options = new BatchProcessingOptions();

        options.EnableMetrics.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_EnableDeadLetter_To_True()
    {
        var options = new BatchProcessingOptions();

        options.EnableDeadLetter.ShouldBeTrue();
    }

    [Fact]
    public void Should_Default_CompletionStrategy_To_Size()
    {
        var options = new BatchProcessingOptions();

        options.CompletionStrategy.ShouldBe(BatchCompletionStrategy.Size);
    }

    [Fact]
    public void Should_Default_MinBatchSize_To_1()
    {
        var options = new BatchProcessingOptions();

        options.MinBatchSize.ShouldBe(1);
    }

    [Fact]
    public void Should_Default_PreserveOrder_To_False()
    {
        var options = new BatchProcessingOptions();

        options.PreserveOrder.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_Priority_To_Normal()
    {
        var options = new BatchProcessingOptions();

        options.DefaultPriority.ShouldBe(BatchPriority.Normal);
    }
}
