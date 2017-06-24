using System.Collections.Generic;
using System.Linq;

namespace Sqpack {
    public class Folder: FolderBase {
        private readonly Dictionary<uint, File> files;

        internal Folder(uint hash, IEnumerable<File> files) {
            this.Hash = hash;
            var fileArray = files as File[] ?? files.ToArray();
            this.files = new Dictionary<uint, File>(fileArray.Length);
            foreach(var file in fileArray)
                this.files.Add(file.Hash, file);
        }

        public File this[uint hash] => this.files[hash];

        public File FindFile(string fileName) {
            return this.files.TryGetValue(FFCrc.Compute(fileName), out File result) ? result : null;
        }
    }
}
