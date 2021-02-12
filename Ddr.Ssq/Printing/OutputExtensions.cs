﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Ddr.Ssq.StepType;
using static Ddr.Ssq.SoloStepType;

namespace Ddr.Ssq.Printing
{
    public static class OutputExtensions
    {
        /// <summary>
        /// Write Text Chunk Summary.
        /// see <a href="https://github.com/pumpCurry/ssqcheck/blob/bb5a9a8181beae7af681612c5f85152f2548cfaa/ssqcheck.php#L68-L109">pumpCurry/ssqcheck/ssqcheck.php</a>
        /// </summary>
        /// <param name="Writer"></param>
        /// <param name="Chunks"></param>
        public static void WriteChunckSummary(this TextWriter Writer, IEnumerable<Chunk> Chunks)
        {
            const string BORDER = "+----+------+--------------+-------------------------+-----------------------------------+------------------+";
            const string HEADER = "|#   |Addr: |Length:( HEX )|Chunk type:              |Values                             |Entry             |";
            var Builder = new StringBuilder();
            try
            {
                Builder.AppendLine($"{BORDER}{Environment.NewLine}{HEADER}{Environment.NewLine}{BORDER}");
                foreach (var (Chunk, Index) in Chunks.Select((v, i) => (v, i)))
                {
                    Builder.Append($"[{Index + 1,3}]");
                    Builder.Append($"[{Chunk.Offset,5:X}][{Chunk.Header.Length,6} ({Chunk.Header.Length,5:X})]");
                    Builder.Append($"[{(short)Chunk.Header.Type:X2}:{Chunk.Header.Type.ToMemberName(),-20}]");
                    Builder.Append(Chunk.Header.Type switch
                    {
                        ChunkType.Tempo_TFPS_Config
                            => $"[TfPS  : ({Chunk.Header.Param,4:X}) {Chunk.Header.Param,4}] {string.Empty,14}",
                        ChunkType.Bigin_Finish_Config
                            => $"[param : ({Chunk.Header.Param,4:X}) {Chunk.Header.Param,4}] {string.Empty,14}",
                        ChunkType.StepData
                            => $"[level : ({Chunk.Header.Param,4:X4}) {Chunk.Header.PlayStyle.ToMemberName(),-8} {Chunk.Header.PlayDifficulty.ToMemberName(),-10}]",
                        _
                            => $"[param : ({Chunk.Header.Param,4:X}) {Chunk.Header.Param,4}] {string.Empty,14}",
                    });
                    Builder.Append($"[Entry: {Chunk.Header.Entry,4} ({Chunk.Header.Entry,4:X})]");
                    Builder.AppendLine();
                    if (Chunk.Header.Type is ChunkType.EndOfFile)
                        break;
                }
                Builder.Append(BORDER);
            }
            finally
            {
                Writer.WriteLine(Builder.ToString());
                Writer.Flush();
            }
        }
        static readonly Dictionary<Encoding, CharMappingType> MappingType = new();
        public enum CharMappingType {
            Unicode =0,
            ANSI = 1,
            ASCII = 2,
        }
        /// <summary>
        /// Write Text Chunk Body Info,
        /// see <a href="https://github.com/pumpCurry/ssqcheck/blob/bb5a9a8181beae7af681612c5f85152f2548cfaa/ssqcheck.php#L265-L446">pumpCurry/ssqcheck/ssqcheck.php</a>
        /// </summary>
        /// <param name="Writer"></param>
        /// <param name="Chunk"></param>
        public static void WriteChunkBodyInfo(this TextWriter Writer, Chunk Chunk)
        {
            var Builder = new StringBuilder();
            try
            {
                Builder.AppendLine(Chunk.Header.Type switch
                {
                    ChunkType.StepData
                        => $"[[[{(short)Chunk.Header.Type,2:X2}:{Chunk.Header.Type.ToMemberName(),-20}]]] [level : ({Chunk.Header.Param,4:X4}) {Chunk.Header.PlayStyle.ToMemberName(),-8} {Chunk.Header.PlayDifficulty.ToMemberName(),-10}]",
                    _
                        => $"[[[{(short)Chunk.Header.Type,2:X2}:{Chunk.Header.Type.ToMemberName(),-20}]]]",
                });
                if(!MappingType.TryGetValue(Writer.Encoding, out var Type)){
                    if(Writer.Encoding.GetString(Writer.Encoding.GetBytes("↗")) == "↗"){
                        Type = CharMappingType.Unicode;
                    } else if (Writer.Encoding.GetString(Writer.Encoding.GetBytes("／")) == "／")
                    {
                        Type = CharMappingType.ANSI;
                    } else {
                        Type = CharMappingType.ASCII;
                    }
                    MappingType.TryAdd(Writer.Encoding, Type);
                }
                var Lines = Chunk.Header.Type switch
                {
                    ChunkType.EndOfFile => Enumerable.Empty<string>(),
                    ChunkType.Tempo_TFPS_Config => Tempo_TFPS_ConfigToFormatEnumerable(Chunk),
                    ChunkType.Bigin_Finish_Config => Bigin_Finish_ConfigToFormatEnumerable(Chunk),
                    ChunkType.StepData => StepDataToFormatEnumerable(Chunk, Type),
                    _ => Enumerable.Empty<string>(),
                };
                foreach (var Line in Lines)
                    Builder.AppendLine(Line);
            }
            finally
            {
                Writer.WriteLine(Builder.ToString());
                Writer.Flush();
            }
        }
        /// <summary>
        /// output <see cref="ChunkType.Tempo_TFPS_Config"/> body information.
        /// </summary>
        /// <param name="Chunk"></param>
        /// <returns></returns>
        public static IEnumerable<string> Tempo_TFPS_ConfigToFormatEnumerable(Chunk Chunk)
        {
            const ChunkType BASE_TYPE = ChunkType.Tempo_TFPS_Config;
            if (Chunk.Header.Type is not BASE_TYPE)
                throw new ArgumentException($"{nameof(Chunk)} type is not {BASE_TYPE}({(short)BASE_TYPE:X}). {nameof(Chunk.Header.Type)} : {Chunk.Header.Type}({(short)Chunk.Header.Type:X})", nameof(Chunk));

            for (var i = 0; i < Chunk.Header.Entry; i++)
            {
                if (i is 0) continue;
                var TimeOffset = Chunk.TimeOffsets[i];
                var LastTimeOffset = Chunk.TimeOffsets[i - 1];
                var DeltaOffset = TimeOffset - LastTimeOffset;
                var DeltaTicks = Chunk.Tempo_TFPS_Config[i] - Chunk.Tempo_TFPS_Config[i - 1];

                var TfPS = Chunk.Header.Param;
                var MeasureLength = 4096;

                var bpm = (DeltaOffset / (double)MeasureLength) / ((DeltaTicks / (double)TfPS) / 240);

                yield return $"[01:BPM][({LastTimeOffset,8:X}:{TimeOffset,8:X}) {LastTimeOffset,8}:{TimeOffset,8}][BPM:{bpm:F5}] Delta> Offset:({DeltaOffset,6:X}){DeltaOffset,7} / Ticks:({DeltaTicks,5:X}){DeltaTicks,7} ";
            }
        }
        /// <summary>
        /// output <see cref="ChunkType.Bigin_Finish_Config"/> body information.
        /// </summary>
        /// <param name="Chunk"></param>
        /// <returns></returns>
        public static IEnumerable<string> Bigin_Finish_ConfigToFormatEnumerable(Chunk Chunk)
        {
            const ChunkType BASE_TYPE = ChunkType.Bigin_Finish_Config;
            if (Chunk.Header.Type is not BASE_TYPE)
                throw new ArgumentException($"{nameof(Chunk)} type is not {BASE_TYPE}({(short)BASE_TYPE:X}). {nameof(Chunk.Header.Type)} : {Chunk.Header.Type}({(short)Chunk.Header.Type:X})", nameof(Chunk));

            for (var i = 0; i < Chunk.Header.Entry; i++)
            {
                var TimeOffset = Chunk.TimeOffsets[i];
                var LastTimeOffset = i is 0 ? 0 : Chunk.TimeOffsets[i - 1];

                var DeltaOffset = i is 0 ? 0 : TimeOffset - LastTimeOffset;
                var ConfigType = Chunk.Bigin_Finish_Config[i];
                yield return $"[02:BFC][({TimeOffset,6:X}) {TimeOffset,8}][func.{(short)ConfigType,4:X}: {Chunk.Bigin_Finish_Config[i].ToMemberName(),-18} ] Delta> Offset:({DeltaOffset,6:X}){DeltaOffset,7} ";
            }
        }
        /// <summary>
        /// output <see cref="ChunkType.StepData"/> body information.
        /// </summary>
        /// <param name="Chunk"></param>
        /// <returns></returns>
        public static IEnumerable<string> StepDataToFormatEnumerable(Chunk Chunk, CharMappingType type = default)
        {
            const ChunkType BASE_TYPE = ChunkType.StepData;
            if (Chunk.Header.Type is not BASE_TYPE)
                throw new ArgumentException($"{nameof(Chunk)} type is not {BASE_TYPE}({(short)BASE_TYPE:X}). {nameof(Chunk.Header.Type)} : {Chunk.Header.Type}({(short)Chunk.Header.Type:X})", nameof(Chunk));
            string LeftArrow;
            string RightArrow;
            string UpArrow;
            string DownArrow;
            string NorthEastArrow;
            string NorthWestArrow;
            string ShockArrow;
            switch (type) {
                default:
                case CharMappingType.Unicode:
                    LeftArrow = "←";
                    RightArrow = "→";
                    UpArrow = "↑";
                    DownArrow = "↓";
                    NorthEastArrow = "↗";
                    NorthWestArrow = "↖";
                    ShockArrow = "◆";
                break;
                case CharMappingType.ASCII:
                case CharMappingType.ANSI:
                    LeftArrow = "←";
                    RightArrow = "→";
                    UpArrow = "↑";
                    DownArrow = "↓";
                    NorthEastArrow = "／";
                    NorthWestArrow = "＼";
                    ShockArrow = "◆";
                break;
            }
            var lastStep = new Queue<byte>();
            var lastCount = 0;

            for (var i = 0; i < Chunk.Header.Entry; i++)
            {
                var TimeOffset = Chunk.TimeOffsets[i];
                var LastTimeOffset = i is 0 ? 0 : Chunk.TimeOffsets[i - 1];
                var DeltaOffset = i is 0 ? 0 : TimeOffset - LastTimeOffset;
                var Step = Chunk.Header.PlayStyle switch
                {
                    PlayStyle.Single
                        => new[] { "　", "…", "…", "…", "…", "　", "　", "　", "　", "　", "　", "　", ":  " },
                    PlayStyle.Solo
                        => new[] { "…", "…", "…", "…", "…", "…", "　", "　", "　", "　", "　", "　", ":  " },
                    PlayStyle.Double
                        => new[] { "　", "…", "…", "…", "…", "　", "　", "…", "…", "…", "…", "　", ":  " },
                    PlayStyle.Battle
                        => new[] { "　", "…", "…", "…", "…", "　", "　", "…", "…", "…", "…", "　", ":  " },
                    _
                        => new[] { "　", "　", "　", "　", "　", "　", "　", "　", "　", "　", "　", "　", ":  " },
                };
                var _step = Chunk.StepData[i];
                Step[12] = $":{_step:X2}";
                if (_step is 0)
                // freeze arrow判定
                {
                    var l = lastStep.Any() ? lastStep.Dequeue() : 0;
                    Step[l] = "＃";
                    var p = l switch
                    {
                        7 or 8 or 9 or 10 => 6,
                        1 or 2 or 3 or 4 or _ => 5,
                    };
                    Step[p] = $"L{(lastCount - lastStep.Count)}";
                }
                else
                // nomal arrow or shock arrow
                {
                    lastStep.Clear();
                    lastCount = 0;
                    if (Chunk.Header.PlayStyle
                        is PlayStyle.Single
                        or PlayStyle.Double
                        or PlayStyle.Battle) //solo以外
                    {
                        var s = (StepType)_step;
                        if ((s & Player1Left) > 0) { Step[1] = LeftArrow; lastStep.Enqueue(1); lastCount++; }
                        if ((s & Player1Down) > 0) { Step[2] = DownArrow; lastStep.Enqueue(2); lastCount++; }
                        if ((s & Player1Up) > 0) { Step[3] = UpArrow; lastStep.Enqueue(3); lastCount++; }
                        if ((s & Player1Right) > 0) { Step[4] = RightArrow; lastStep.Enqueue(4); lastCount++; }
                        if ((s & Player1Special) == Player1Special) // Shock Arrow
                        { Step[1] = ShockArrow; Step[2] = ShockArrow; Step[3] = ShockArrow; Step[4] = ShockArrow; Step[5] = ShockArrow; }

                        if (i < Chunk.Header.Entry - 1)
                        {
                            if ((s & Player1Special) > 0 && Chunk.StepData[i + 1] == 0x0) // freeze arrow 
                            { Step[5] = "長"; }
                        }
                    }
                    if (Chunk.Header.PlayStyle
                        is PlayStyle.Solo)
                    {
                        var s = (SoloStepType)_step;
                        if ((s & SoloPlayerLeft) > 0) { Step[0] = LeftArrow; }
                        if ((s & SoloPlayerNorthWest) > 0) { Step[1] = NorthWestArrow; }
                        if ((s & SoloPlayerDown) > 0) { Step[2] = DownArrow; }
                        if ((s & SoloPlayerUp) > 0) { Step[3] = UpArrow; }
                        if ((s & SoloPlayerNorthEast) > 0) { Step[4] = NorthEastArrow; }
                        if ((s & SoloPlayerRight) > 0) { Step[5] = RightArrow; }
                    }

                    if (Chunk.Header.PlayStyle
                        is PlayStyle.Double
                        or PlayStyle.Battle)
                    {
                        var s = (StepType)_step;
                        if ((s & Player2Left) > 0) { Step[7] = LeftArrow; lastStep.Enqueue(7); lastCount++; }
                        if ((s & Player2Down) > 0) { Step[8] = DownArrow; lastStep.Enqueue(8); lastCount++; }
                        if ((s & Player2Up) > 0) { Step[9] = UpArrow; lastStep.Enqueue(9); lastCount++; }
                        if ((s & Player2Right) > 0) { Step[10] = RightArrow; lastStep.Enqueue(10); lastCount++; }

                        if ((s & Player2Special) == Player2Special) //Shock Arrow
                        { Step[7] = ShockArrow; Step[8] = ShockArrow; Step[9] = ShockArrow; Step[10] = ShockArrow; Step[6] = "衝"; }

                        if (i < Chunk.Header.Entry - 1)
                        {
                            if ((s & Player2Special) > 0 && Chunk.StepData[i + 1] == 0x0) // freeze arrow 
                            { Step[6] = "長"; }
                        }
                    }
                }
                yield return $"[03:STP][({TimeOffset,6:X}) {TimeOffset,8}][{string.Join("", Step)}] Delta> Offset:({DeltaOffset,6:X}){DeltaOffset,7} ";
            }
        }
    }
}
