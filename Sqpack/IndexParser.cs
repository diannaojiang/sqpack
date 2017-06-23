using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SegmentHeader = System.Tuple<int, long, int, int, string>;
using FileSegmentHeader = System.Tuple<uint, uint, int>;

namespace Sqpack {
    public class IndexParser: ParserBase {
        private SegmentHeader[] _segments;

        protected override int Type => 2;

        private IEnumerable<SegmentHeader> ParseSegmentHeaders() {
            var streamStart = this.Stream.Position;
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

            var lengthBytes = ReadData(4);
            var length = lengthBytes.ToInt32();
            dataToHash = new List<byte>(length);
            dataToHash.AddRange(lengthBytes);

            var total = length / 72 - 1;
            for(var i = 1; i <= total; i++) {
                var start = streamStart + offset;
                ReadData(4, true);
                var segmentOffset = ReadData(4, true).ToInt32();
                var segmentSize = ReadData(4, true).ToInt32();
                var hash = ReadData(20, true).ToHex();
                ReadData(i == 1 ? 44 : 40, true);
                if(segmentOffset > 0 || segmentSize > 0)
                    yield return Tuple.Create(i, start, segmentOffset, segmentSize, hash);
            }

            ReadData(length - 64 - offset, true);
            if(ReadData(20).ToHex() != dataToHash.ToArray().ToSha1())
                throw new Exception("Segment header data hash mismatch.");
            ReadData(length - offset);
        }

        private IEnumerable<uint> ParseFolderSegment(int offset, int size, string hash) {
            var data = this.GetStreamData(offset, size, hash);
            using(var reader = new BinaryReader(new MemoryStream(data)))
                while(true) {
                    var crc = reader.ReadUInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt32();
                    yield return crc;
                    if(reader.BaseStream.Position == reader.BaseStream.Length)
                        yield break;
                }
        }

        private IEnumerable<FileSegmentHeader> ParseFileSegment(int offset, int size, string hash) {
            var data = this.GetStreamData(offset, size, hash);
            using(var reader = new BinaryReader(new MemoryStream(data)))
                while(true) {
                    var crc = reader.ReadUInt32();
                    var folderCrc = reader.ReadUInt32();
                    var fileOffset = reader.ReadInt32() * 8;
                    reader.ReadInt32();
                    yield return Tuple.Create(crc, folderCrc, fileOffset);
                    if(reader.BaseStream.Position == reader.BaseStream.Length)
                        yield break;
                }
        }

        public IndexParser(Stream stream): base(stream) {
            this.ParseHeader();
        }

        public IEnumerable<Folder> GetFolders() {
            this._segments = this._segments ?? this.ParseSegmentHeaders().ToArray();
            var segment = this._segments.First(s => s.Item1 == 4);
            var folders = this.ParseFolderSegment(segment.Item3, segment.Item4, segment.Item5);
            segment = this._segments.First(s => s.Item1 == 1);
            var files = this.ParseFileSegment(segment.Item3, segment.Item4, segment.Item5);
            return folders.Select(folder => new Folder(folder,
                files.Where(file => file.Item2 == folder)
                     .Select(file => new File(file.Item1, file.Item3))));
        }
    }
}
