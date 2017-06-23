using System;
using System.Collections.Generic;
using System.IO;

namespace Sqpack {
    public abstract class ParserBase {
        protected Stream Stream {
            get;
            set;
        }

        protected abstract int Type {
            get;
        }

        protected ParserBase(Stream stream) {
            this.Stream = stream;
        }

        protected byte[] GetStreamData(int position, int size, string hash = null) {
            var result = new byte[size];
            this.Stream.Seek(position, SeekOrigin.Begin);
            this.Stream.Read(result, 0, result.Length);
            if(hash != null && result.ToSha1() != hash)
                throw new Exception("Segment data hash mismatch.");
            return result;
        }

        protected void ParseHeader() {
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

            // Signature
            var signature = ReadData(12);
            if(signature.ToHex() != "53715061636b000000000000")
                throw new Exception("Invalid Sqpack file.");
            // Header Length
            var lengthBytes = ReadData(4);
            var length = lengthBytes.ToInt32();
            dataToHash = new List<byte>(length);
            dataToHash.AddRange(signature);
            dataToHash.AddRange(lengthBytes);
            // Unknown
            ReadData(4, true);
            // SqPack Type
            var data = ReadData(4, true);
            if(data.ToInt32() != this.Type)
                throw new Exception("Invalid Sqpack file type.");
            // Unknown
            ReadData(936, true);
            // SHA1 of Header
            data = ReadData(20);
            if(data.ToHex() != dataToHash.ToArray().ToSha1())
                throw new Exception("Header data hash mismatch.");
            // Padding
            ReadData(length - offset);
        }
    }
}
