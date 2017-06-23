using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zlib;
using DataEntryHeader = System.Tuple<int, int, int>;
using Type2Block = System.Tuple<int, short, short>;
using Type2BlockHeader = System.Tuple<long, int, int>;

namespace Sqpack {
    public class DatParser: ParserBase {
        protected override int Type => 1;

        private void ParseDataHeaders() {
            var offset = 0;
            List<byte> dataToHash = null;

            byte[] ReadData(int size, bool addToHash = false) {
                var result = new byte[size];
                if(this.Stream.Read(result, 0, result.Length) != size)
                    throw new Exception("Meet unexpected EOF.");
                offset += size;
                if(addToHash)
                    // ReSharper disable once PossibleNullReferenceException
                    dataToHash.AddRange(result);
                return result;
            }

            // Header Length
            var lengthBytes = ReadData(4);
            var length = lengthBytes.ToInt32();
            dataToHash = new List<byte>(length);
            dataToHash.AddRange(lengthBytes);
            // Unknown
            ReadData(8, true);
            // Data Size
            var dataSize = ReadData(4, true).ToInt32() * 8 /* * 16 */;
            // Spanned DAT
            var spannedDat = ReadData(4, true).ToInt32();
            // Unknown
            ReadData(4, true);
            // Max File Size
            ReadData(4, true);
            // Unknown
            ReadData(4, true);
            // SHA1 of Data
            var dataSha1 = ReadData(20, true).ToHex();
            // Padding
            ReadData(length - 64 - offset, true);
            // SHA1 of Header
            if(ReadData(20).ToHex() != dataToHash.ToArray().ToSha1())
                throw new Exception("Segment header data hash mismatch.");
            // Padding
            ReadData(length - offset);
        }

        private DataEntryHeader ParseDataEntryHeader(out object blockTable) {
            var offset = 0;

            byte[] ReadData(int size) {
                var result = new byte[size];
                if(this.Stream.Read(result, 0, result.Length) != size)
                    throw new Exception("Meet unexpected EOF.");
                // ReSharper disable once AccessToModifiedClosure
                offset += size;
                return result;
            }

            // Header Length
            var length = ReadData(4).ToInt32();
            // Content Type
            var contentType = ReadData(4).ToInt32();
            // Uncompressed Size
            var uncompressedSize = ReadData(4).ToInt32();
            // Unknown
            ReadData(4);
            // Block Buffer Size
            var blockBufferSize = ReadData(4).ToInt32();
            // Num Blocks
            var numberOfBlocks = ReadData(4).ToInt32();

            IEnumerable<Type2Block> ParseType2BlockTable() {
                for(var i = 0; i < numberOfBlocks; i++)
                    yield return Tuple.Create(ReadData(4).ToInt32(), ReadData(2).ToInt16(), ReadData(2).ToInt16());
            }

            switch(contentType) {
                case 2:
                    blockTable = ParseType2BlockTable().ToArray();
                    break;
                default:
                    blockTable = null;
                    break;
            }

            // Padding
            ReadData(length - offset);

            return Tuple.Create(contentType, uncompressedSize, blockBufferSize);
        }

        private Type2BlockHeader ParseType2BlockHeader(long start) {
            this.Stream.Seek(start, SeekOrigin.Begin);

            var offset = 0;

            byte[] ReadData(int size) {
                var result = new byte[size];
                if(this.Stream.Read(result, 0, result.Length) != size)
                    throw new Exception("Meet unexpected EOF.");
                // ReSharper disable once AccessToModifiedClosure
                offset += size;
                return result;
            }

            // Header Length
            var length = ReadData(4).ToInt32();
            // Unknown
            ReadData(4);
            // Compressed Length
            var compressedLength = ReadData(4).ToInt32();
            // Decompressed Length
            var decompressedLength = ReadData(4).ToInt32();
            // Padding
            ReadData(length - offset);

            return Tuple.Create(this.Stream.Position, compressedLength, decompressedLength);
        }

        private byte[] GetType2BlockData(long start, int compressedLength, int decompressedLength) {
            this.Stream.Seek(start, SeekOrigin.Begin);

            var isCompressed = compressedLength < 32000;
            var data = new byte[isCompressed ? compressedLength : decompressedLength];
            this.Stream.Read(data, 0, data.Length);
            if(isCompressed)
                data = DeflateStream.UncompressBuffer(data);

            return data;
        }

        private byte[] CombineType2BlockData(long start, IReadOnlyList<Type2Block> blockTable) {
            var result = blockTable.Select(table => this.ParseType2BlockHeader(start + table.Item1))
                                   .SelectMany(header => this.GetType2BlockData(header.Item1, header.Item2, header.Item3))
                                   .ToArray();

            if(result.Length != blockTable.Sum(block => block.Item3))
                throw new Exception("Data size mismatch");

            return result;
        }

        public DatParser(Stream stream): base(stream) {
            this.ParseHeader();
            this.ParseDataHeaders();
        }

        public byte[] GetFileData(int offset) {
            this.Stream.Seek(offset, SeekOrigin.Begin);

            (var contentType, var uncompressedSize, var blockBufferSize) = this.ParseDataEntryHeader(out var blockTable);
            switch(contentType) {
                case 2:
                    // Binary
                    return this.CombineType2BlockData(this.Stream.Position, (Type2Block[])blockTable);
                default:
                    throw new Exception("Unsupported file type: " + contentType);
            }
        }
    }
}
