// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;

namespace Excalibur.Integration.Tests.Data.Inbox;

[Trait("Category", "Unit")]
[Trait("Component", "Sample.OrderSubmission")]
[Trait("Pattern", "Regression")]
public sealed class CommerceCommitFrameParserShould
{
    [Fact]
    public void RecognizeAFragmentedRealShapeAndRefuseMalformedFraming()
    {
        byte[] commit = [14, 1, 0, 34, 0, 0, 1, 0, 22, 0, 0, 0, 18, 0, 0, 0, 2, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 7, 0, 0, 0];
        var parser = new CommerceCommitFrameParser();
        for (var index = 0; index < commit.Length - 1; index++)
        {
            parser.Append(commit.AsMemory(index, 1)).ShouldBeEmpty();
        }

        var message = parser.Append(commit.AsMemory(commit.Length - 1, 1)).Single();
        message.IsCommit.ShouldBeTrue();
        message.Wire.ShouldBe(commit);
        var malformed = (byte[])commit.Clone();
        malformed[3] = 7;
        Should.Throw<InvalidDataException>(() => new CommerceCommitFrameParser().Append(malformed));
        var zeroDescriptor = (byte[])commit.Clone();
        zeroDescriptor[18] = 0;
        Should.Throw<InvalidDataException>(() => new CommerceCommitFrameParser().Append(zeroDescriptor));
        var duplicateTrace = new byte[86];
        commit.AsSpan(0, 30).CopyTo(duplicateTrace);
        duplicateTrace[3] = 86;
        BinaryPrimitives.WriteUInt32LittleEndian(duplicateTrace.AsSpan(8), 74);
        BinaryPrimitives.WriteUInt32LittleEndian(duplicateTrace.AsSpan(30), 26);
        BinaryPrimitives.WriteUInt16LittleEndian(duplicateTrace.AsSpan(34), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(duplicateTrace.AsSpan(56), 26);
        BinaryPrimitives.WriteUInt16LittleEndian(duplicateTrace.AsSpan(60), 3);
        commit.AsSpan(30, 4).CopyTo(duplicateTrace.AsSpan(82));
        Should.Throw<InvalidDataException>(() => new CommerceCommitFrameParser().Append(duplicateTrace))
            .Message.ShouldBe("Repeated trace activity header.");
    }
}
