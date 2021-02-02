﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Ssq
{
    public class ChunkReader : IDisposable
    {
        Stream Stream;
        readonly bool LeaveOpen;
        public ChunkReader(Stream Stream) : this(Stream, false) { }
        public ChunkReader(Stream Stream, bool LeaveOpen)
            => (this.Stream, this.LeaveOpen) = (Stream, LeaveOpen);
        public IEnumerable<Chunk> ReadToEnd()
        {
            var Length = Stream.Length;
            while (Stream.Position < Length)
            {
                var Chunk = ReadChunk();
                yield return Chunk;
                if (Chunk.Header.Type is ChunkType.EndOfFile)
                    yield break;
            }
        }
        public Chunk ReadChunk()
        {
            var Chunk = new Chunk();
            ReadHeader(Chunk);
            ReadBody(Chunk);
            return Chunk;
        }
        ChunkHeader ReadHeader()
        {
            var headerSize = Marshal.SizeOf<ChunkHeader>();
            var buffer = ArrayPool<byte>.Shared.Rent(headerSize);
            try
            {
                var span = buffer.AsSpan();
                var readed = Stream.Read(span);
                if (readed != headerSize)
                    throw new InvalidOperationException();
                return MemoryMarshal.Read<ChunkHeader>(span);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        void ReadHeader(Chunk Chunk)
        {
            var Offset = Stream.Position;
            var Header = ReadHeader();
            Chunk.Offset = Offset;
            Chunk.Header = Header;
        }
        void ReadBody(Chunk Chunk)
        {
            if (Chunk.Header is { Type: ChunkType.EndOfFile } or { Length: 0 })
                return;
            var Entry = Chunk.Header.Entry;
            var Length = Chunk.Header.Length;
            var Size = Marshal.SizeOf<ChunkHeader>();
            //timeOffsetのリストを生成
            switch (Chunk.Header.Type)
            {
                //case ChunkType.EndOfFile:
                //    return;
                default:
                case ChunkType.Tempo_TFPS_Config:
                case ChunkType.Bigin_Finish_Config:
                case ChunkType.StepData:
                    {
                        Size += Entry * sizeof(uint);
                        if (Size > Length)
                            throw new InvalidOperationException("over size exception.");
                        using var Reader = new BinaryReader(Stream, Encoding.UTF8, true);
                        var TimeOffsets = new uint[Entry];
                        for (var i = 0; i < Entry; i++)
                        {
                            TimeOffsets[i] = Reader.ReadUInt32();
                        }
                        Chunk.TimeOffsets = TimeOffsets;
                    }
                    break;
            }
            switch (Chunk.Header.Type)
            {
                //case ChunkType.EndOfFile:
                //    return;
                case ChunkType.Tempo_TFPS_Config:
                    {
                        Size += Entry * sizeof(int);
                        if (Size > Length)
                            throw new InvalidOperationException("over size exception.");
                        using var Reader = new BinaryReader(Stream, Encoding.UTF8, true);
                        var Tempo_TFPS_Config = new int[Entry];
                        for (var i = 0; i < Entry; i++)
                        {
                            Tempo_TFPS_Config[i] = Reader.ReadInt32();
                        }
                        Chunk.Tempo_TFPS_Config = Tempo_TFPS_Config;
                    }
                    break;
                case ChunkType.Bigin_Finish_Config:
                    {
                        Size += Entry * sizeof(short);
                        if (Size > Length)
                            throw new InvalidOperationException("over size exception.");
                        using var Reader = new BinaryReader(Stream, Encoding.UTF8, true);
                        var Bigin_Finish_Config = new short[Entry];
                        for (var i = 0; i < Entry; i++)
                        {
                            Bigin_Finish_Config[i] = Reader.ReadInt16();
                        }
                        Chunk.Bigin_Finish_Config = Bigin_Finish_Config;
                    }
                    break;
                case ChunkType.StepData:
                    {
                        Size += Entry * sizeof(byte);
                        if (Size > Length)
                            throw new InvalidOperationException("over size exception.");
                        using var Reader = new BinaryReader(Stream, Encoding.UTF8, true);
                        var StepData = new byte[Entry];
                        for (var i = 0; i < Entry; i++)
                        {
                            StepData[i] = Reader.ReadByte();
                        }
                        Chunk.StepData = StepData;
                    }
                    break;
                default:
                    {
                        Size += Entry * sizeof(byte);
                        if (Size > Length)
                            throw new InvalidOperationException("over size exception.");
                        using var Reader = new BinaryReader(Stream, Encoding.UTF8, true);
                        var OtherData = new byte[Entry];
                        for (var i = 0; i < Entry; i++)
                        {
                            OtherData[i] = Reader.ReadByte();
                        }
                        Chunk.OtherData = OtherData;
                    }
                    break;
            }
        }
        public void Dispose()
        {
            if (!LeaveOpen)
                Stream.Dispose();
        }
    }
}
