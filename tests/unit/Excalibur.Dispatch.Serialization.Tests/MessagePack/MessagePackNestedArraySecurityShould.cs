// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using MessagePack;

using FrameworkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Pattern", "Security")]
public sealed class MessagePackNestedArraySecurityShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [RequiresUnreferencedCode("Exercises the MessagePack runtime object formatter.")]
    [RequiresDynamicCode("Exercises the MessagePack runtime object formatter.")]
    public void RejectNestedArrayLengthReuseWithoutAmplifyingAllocation(bool runtimeTypeApi)
    {
        var options = MessagePackSerializerOptions.Standard.WithSecurity(
            MessagePackSecurity.UntrustedData.WithMaximumObjectGraphDepth(1));
        var serializer = new FrameworkSerializer(options);
        byte[] payload = CreateNestedArrayLengthReusePayload();
        Action deserialize = runtimeTypeApi
            ? () => serializer.DeserializeObject(payload, typeof(object))
            : () => serializer.Deserialize<object>(payload);

        byte[] shallowPayload = [0x91, 0x01];
        object shallow = runtimeTypeApi
            ? serializer.DeserializeObject(shallowPayload, typeof(object))
            : serializer.Deserialize<object>(shallowPayload);
        shallow.ShouldBeOfType<object[]>()[0].ShouldBe((byte)1);

        // Warm formatter resolution and exception paths outside the allocation interval.
        Should.Throw<SerializationException>(deserialize);
        long before = GC.GetAllocatedBytesForCurrentThread();
        SerializationException? rejection = null;
        try
        {
            deserialize();
        }
        catch (SerializationException exception)
        {
            rejection = exception;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        rejection.ShouldNotBeNull();
        rejection.InnerException.ShouldBeOfType<MessagePackSerializationException>()
            .InnerException.ShouldBeOfType<EndOfStreamException>();
        // One allowed 8K-element reference array plus input copying and exceptions fits this
        // ceiling. Reusing the remaining bytes to allocate another such array does not.
        allocated.ShouldBeLessThan(96 * 1024);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [RequiresUnreferencedCode("Exercises the MessagePack runtime object formatter.")]
    [RequiresDynamicCode("Exercises the MessagePack runtime object formatter.")]
    public void PreserveValidNestedObjectArrays(bool runtimeTypeApi)
    {
        var serializer = new FrameworkSerializer();
        byte[] payload = [0x92, 0x91, 0x01, 0x02];

        object value = runtimeTypeApi
            ? serializer.DeserializeObject(payload, typeof(object))
            : serializer.Deserialize<object>(payload);

        var outer = value.ShouldBeOfType<object[]>();
        outer.Length.ShouldBe(2);
        outer[0].ShouldBeOfType<object[]>()[0].ShouldBe((byte)1);
        outer[1].ShouldBe((byte)2);
    }

    private static byte[] CreateNestedArrayLengthReusePayload()
    {
        const int width = 8192;
        const int layers = 4;
        var payload = new byte[(layers * 5) + width];
        for (int layer = 0; layer < layers; layer++)
        {
            int offset = layer * 5;
            payload[offset] = 0xdd; // MessagePack array32.
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(offset + 1, 4), width);
        }

        payload.AsSpan(layers * 5).Fill(0xc0); // Nil values; deliberately insufficient for all declared containers.
        return payload;
    }
}
