using System;
using System.Linq;

namespace Sqpack {
    public abstract class FolderBase {
        public uint Hash {
            get;
            protected set;
        }

        public override string ToString() {
            return BitConverter.GetBytes(this.Hash).Reverse().ToArray().ToHex();
        }
    }
}
