// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Core.Network
{
    /// <summary>
    /// Error when reading a received network wire message.
    /// </summary>
    class WireMessageReadException : Exception
    {
        public WireMessageReadException(string message) : base(message)
        {
        }
        public WireMessageReadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// A buffer into which application reads network data in network-wire-format. Application
    /// may then read parsed objects from the buffer.
    /// </summary>
    class WireMessageReadBuffer
    {
        byte[] _buffer;
        int _readCursor;
        int _writeCursor;
        bool _ongoingReceive;
        int _receiveBufferSize;

        public WireMessageReadBuffer()
        {
            _buffer = new byte[8192];
            _receiveBufferSize = WireProtocol.PacketHeaderSize;
        }

        /// <summary>
        /// Append data into buffer.
        /// </summary>
        public void OnReceivedData(Span<byte> data)
        {
            EnsureWriteCapacity(data.Length);
            data.CopyTo(_buffer.AsSpan(start: _writeCursor));
            _writeCursor += data.Length;
        }

        /// <summary>
        /// Get buffer into which append data into.
        /// </summary>
        public void BeginReceiveData(out byte[] array, out int offset, out int count)
        {
            if (_ongoingReceive)
                throw new InvalidOperationException("Already ongoing receive");

            EnsureWriteCapacity(_receiveBufferSize);
            array = _buffer;
            offset = _writeCursor;
            count = _buffer.Length - _writeCursor;
            _ongoingReceive = true;
        }

        /// <summary>
        /// Complete ongoing append.
        /// </summary>
        public void EndReceiveData(int numBytes)
        {
            if (!_ongoingReceive)
                throw new InvalidOperationException("No ongoing receive");

            _writeCursor += numBytes;
            _ongoingReceive = false;
        }

        /// <summary>
        /// Reads the next message in the buffer and returns true. If there is no ready message in the buffer, return false.
        /// </summary>
        /// <exception cref="WireMessageReadException">If invalid wire data is parsed</exception>
        public bool TryReadNext(out WirePacketType type, out Span<byte> payload, out MetaMessage message)
        {
            int numReceivedBytes = _writeCursor - _readCursor;
            if (numReceivedBytes < WireProtocol.PacketHeaderSize)
            {
                type = WirePacketType.None;
                payload = default;
                message = null;
                return false;
            }

            // Read header
            WirePacketHeader header;
            try
            {
                header = WireProtocol.DecodePacketHeader(_buffer, _readCursor, enforcePacketPayloadSizeLimit: false);
            }
            catch (Exception ex)
            {
                throw new WireMessageReadException("Invalid header", ex);
            }

            int framedSize = WireProtocol.PacketHeaderSize + header.PayloadSize;
            if (numReceivedBytes < framedSize)
            {
                // There's an incomplete message in the buffer. Ensure the read buffer has space for the complete message.
                _receiveBufferSize = System.Math.Max(_receiveBufferSize, framedSize);

                type = WirePacketType.None;
                payload = default;
                message = null;
                return false;
            }

            // Read frame
            byte[]  payloadBuffer;
            int     payloadOffset;
            int     payloadSize;
            switch (header.Compression)
            {
                case WirePacketCompression.None:
                    payloadBuffer = _buffer;
                    payloadOffset = _readCursor + WireProtocol.PacketHeaderSize;
                    payloadSize = header.PayloadSize;
                    break;

                case WirePacketCompression.Deflate:
                    payloadBuffer = CompressUtil.DeflateDecompress(_buffer, _readCursor + WireProtocol.PacketHeaderSize, header.PayloadSize);
                    payloadOffset = 0;
                    payloadSize = payloadBuffer.Length;
                    break;

                default:
                    throw new WireMessageReadException($"Invalid CompressionMode in packet: {header.Compression}");
            }

            // Bump read cursor. If all is read, move cursors to
            // the beginning (trivial compatcting). This is ok since
            // the actual data payload remains valid.
            _readCursor += WireProtocol.PacketHeaderSize + header.PayloadSize;
            if (_readCursor == _writeCursor)
            {
                _readCursor = 0;
                _writeCursor = 0;
            }

            // Parse the frame
            switch (header.Type)
            {
                case WirePacketType.Message:
                {
                    try
                    {
                        message = WireProtocol.DecodeMessage(payloadBuffer, payloadOffset, payloadSize, resolver: null);
                    }
                    catch (Exception ex)
                    {
                        throw new WireMessageReadException("Invalid message", ex);
                    }
                    type = WirePacketType.Message;
                    payload = default;
                    return true;
                }

                case WirePacketType.Ping:
                {
                    type = WirePacketType.Ping;
                    payload = payloadBuffer.AsSpan(payloadOffset, payloadSize);
                    message = null;
                    return true;
                }

                case WirePacketType.PingResponse:
                {
                    type = WirePacketType.PingResponse;
                    payload = payloadBuffer.AsSpan(payloadOffset, payloadSize);
                    message = null;
                    return true;
                }

                default:
                    throw new WireMessageReadException($"Unrecognized WirePacketType: {header.Type}");
            }
        }

        /// <summary>
        /// Ensures the buffer has <paramref name="numBytesAvailableNeeded"/> bytes available for writing at
        /// the write cursor.
        /// </summary>
        void EnsureWriteCapacity(int numBytesAvailableNeeded)
        {
            int numAvailable = _buffer.Length - _writeCursor;
            if (numAvailable >= numBytesAvailableNeeded)
                return;

            // Try compacting if that is sufficient
            int numBytesUsed = _writeCursor - _readCursor;
            if (numBytesUsed + numBytesAvailableNeeded <= _buffer.Length)
            {
                Buffer.BlockCopy(src: _buffer, srcOffset: _readCursor, dst: _buffer, dstOffset: 0, numBytesUsed);
                _writeCursor -= _readCursor;
                _readCursor = 0;
                return;
            }

            // Allocate a bigger buffer
            int newBufferSizeRequired = numBytesUsed + numBytesAvailableNeeded;
            byte[] newBuffer = new byte[GetBufferSize(newBufferSizeRequired)];
            Buffer.BlockCopy(src: _buffer, srcOffset: _readCursor, dst: newBuffer, dstOffset: 0, numBytesUsed);
            _writeCursor -= _readCursor;
            _readCursor = 0;
            _buffer = newBuffer;
        }

        static int GetBufferSize(int requiredSize)
        {
            // Round to the next 1KB
            return ((requiredSize + 1023) / 1024) * 1024;
        }
    }
}
