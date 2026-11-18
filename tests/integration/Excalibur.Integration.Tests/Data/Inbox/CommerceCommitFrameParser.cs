// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers.Binary;

namespace Excalibur.Integration.Tests.Data.Inbox;

// Test-only, post-login TDS7 framing. It deliberately refuses profiles outside the arm's contract.
internal sealed class CommerceCommitFrameParser
{
    private const int Limit = 65_536;
    private readonly List<byte> _pending = [];
    private readonly List<byte> _wire = [];
    private readonly List<byte> _body = [];
    private int _type = -1;
    private byte _packetId = 1;

    internal readonly record struct Message(byte[] Wire, bool IsCommit);

    internal IReadOnlyList<Message> Append(ReadOnlyMemory<byte> bytes)
    {
        if (_pending.Count + bytes.Length > Limit)
        {
            throw new InvalidDataException("TDS pending input exceeded its test bound.");
        }

        _pending.AddRange(bytes.ToArray());
        var messages = new List<Message>();
        while (_pending.Count >= 8)
        {
            var length = (_pending[2] << 8) | _pending[3];
            if (length < 8 || length > 32_767)
            {
                throw new InvalidDataException("Invalid TDS packet length.");
            }

            if (_pending.Count < length)
            {
                break;
            }

            var packet = _pending.GetRange(0, length).ToArray();
            _pending.RemoveRange(0, length);
            var type = packet[0];
            if (type is not (1 or 3 or 14) || (packet[1] & ~1) != 0 || packet[6] != _packetId)
            {
                throw new InvalidDataException("Unsupported post-login TDS profile, flags or packet sequence.");
            }

            if (_type >= 0 && type != _type)
            {
                throw new InvalidDataException("TDS message changed packet type.");
            }

            _type = type;
            _packetId = unchecked((byte)(_packetId + 1));
            if (_wire.Count + packet.Length > Limit)
            {
                throw new InvalidDataException("TDS message exceeded its test bound.");
            }

            _wire.AddRange(packet);
            _body.AddRange(packet.AsSpan(8).ToArray());
            if ((packet[1] & 1) == 0)
            {
                continue;
            }

            messages.Add(new Message(_wire.ToArray(), type == 14 && IsFinalCommit(_body.ToArray())));
            _wire.Clear();
            _body.Clear();
            _type = -1;
            _packetId = 1;
        }

        return messages;
    }

    private static bool IsFinalCommit(ReadOnlySpan<byte> body)
    {
        if (body.Length < 6)
        {
            throw new InvalidDataException("Truncated transaction-manager request.");
        }

        var total = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (total < 4 || total > body.Length - 2)
        {
            throw new InvalidDataException("Invalid TDS ALL_HEADERS length.");
        }

        var offset = 4;
        var descriptorSeen = false;
        var traceSeen = false;
        while (offset < total)
        {
            if (total - offset < 6)
            {
                throw new InvalidDataException("Truncated TDS stream header.");
            }

            var size = BinaryPrimitives.ReadUInt32LittleEndian(body[offset..]);
            var kind = BinaryPrimitives.ReadUInt16LittleEndian(body[(offset + 4)..]);
            if (size < 6 || size > total - offset || kind is not (2 or 3))
            {
                throw new InvalidDataException("Unsupported TDS stream header.");
            }

            if (kind == 2)
            {
                if (descriptorSeen || size != 18 || BinaryPrimitives.ReadUInt64LittleEndian(body[(offset + 6)..]) == 0)
                {
                    throw new InvalidDataException("Missing, repeated or zero transaction descriptor.");
                }

                descriptorSeen = true;
            }

            if (kind == 3)
            {
                if (traceSeen)
                {
                    throw new InvalidDataException("Repeated trace activity header.");
                }

                traceSeen = true;
            }

            offset += checked((int)size);
        }

        if (!descriptorSeen || offset != total)
        {
            throw new InvalidDataException("No exact transaction descriptor header.");
        }

        var request = BinaryPrimitives.ReadUInt16LittleEndian(body[offset..]);
        if (request != 7)
        {
            return false;
        }

        if (body.Length != offset + 4 || body[offset + 2] != 0 || body[offset + 3] != 0)
        {
            throw new InvalidDataException("Only an unflagged final COMMIT is supported.");
        }

        return true;
    }
}

