using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class MessageBatchShould
{
    [Fact]
    public void Should_Generate_Unique_BatchId_By_Default()
    {
        var batch1 = new MessageBatch();
        var batch2 = new MessageBatch();

        batch1.BatchId.ShouldNotBeNullOrEmpty();
        batch2.BatchId.ShouldNotBeNullOrEmpty();
        batch1.BatchId.ShouldNotBe(batch2.BatchId);
    }

    [Fact]
    public void Should_Default_Messages_To_Empty()
    {
        var batch = new MessageBatch();

        batch.Messages.ShouldNotBeNull();
        batch.Messages.Count.ShouldBe(0);
    }

    [Fact]
    public void Size_Should_Return_Message_Count()
    {
        var batch = new MessageBatch
        {
            Messages = [new TransportMessage(), new TransportMessage(), new TransportMessage()],
        };

        batch.Size.ShouldBe(3);
    }

    [Fact]
    public void SizeInBytes_Should_Calculate_Total_Body_Size()
    {
        var batch = new MessageBatch
        {
            Messages =
            [
                new TransportMessage { Body = new byte[] { 1, 2, 3 } },
                new TransportMessage { Body = new byte[] { 4, 5 } },
            ],
        };

        batch.SizeInBytes.ShouldBe(5);
    }

    [Fact]
    public void SizeInBytes_Should_Handle_Empty_Bodies()
    {
        var batch = new MessageBatch
        {
            Messages =
            [
                new TransportMessage(),
                new TransportMessage { Body = new byte[] { 1 } },
            ],
        };

        batch.SizeInBytes.ShouldBe(1);
    }

    [Fact]
    public void IsEmpty_Should_Be_True_When_No_Messages()
    {
        var batch = new MessageBatch();

        batch.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_Should_Be_False_When_Has_Messages()
    {
        var batch = new MessageBatch { Messages = [new TransportMessage()] };

        batch.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void IsFull_Should_Be_True_When_At_Max()
    {
        var batch = new MessageBatch
        {
            Messages = [new TransportMessage(), new TransportMessage()],
        };

        batch.IsFull(2).ShouldBeTrue();
    }

    [Fact]
    public void IsFull_Should_Be_False_When_Below_Max()
    {
        var batch = new MessageBatch { Messages = [new TransportMessage()] };

        batch.IsFull(5).ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_Priority_To_Normal()
    {
        var batch = new MessageBatch();

        batch.Priority.ShouldBe(BatchPriority.Normal);
    }

    [Fact]
    public void Should_Default_Metadata_To_Empty()
    {
        var batch = new MessageBatch();

        batch.Metadata.ShouldNotBeNull();
        batch.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Default_MaxProcessingTime_To_Null()
    {
        var batch = new MessageBatch();

        batch.MaxProcessingTime.ShouldBeNull();
    }
}
