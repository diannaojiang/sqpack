namespace Sqpack {
    public sealed class File: FolderBase {
        public int Offset {
            get;
            private set;
        }

        public int Dat => (this.Offset & 0xF) % 2;

        internal File(uint hash, int offset) {
            this.Hash = hash;
            this.Offset = offset;
        }
    }
}
