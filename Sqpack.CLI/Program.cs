using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sqpack.CLI {
    public class Program {
        public static void Main(string[] args) {
            var gameDir = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "/mnt/e/Games/最终幻想XIV/"
                : @"E:\Games\最终幻想XIV\";
            var sqpackDir = Path.Combine(gameDir, "game", "sqpack");
            var indexFile = Path.Combine(sqpackDir, "ffxiv", "000000.win32.index");
            var datFile = Path.ChangeExtension(indexFile, ".dat0");

            Folder[] folders = null;
            using(var stream = System.IO.File.OpenRead(indexFile)) {
                var parser = new IndexParser(stream);
                folders = parser.GetFolders().ToArray();
            }

            var file = folders.Select(f => f.FindFile("vulgarwordsfilter.dic")).FirstOrDefault(f => f != null);
            if(file == null)
                return;

            using(var stream = System.IO.File.OpenRead(datFile)) {
                var parser = new DatParser(stream);
                // System.IO.File.WriteAllBytes(@"D:\vulgarwordsfilter.dic.mine", parser.GetFileData(file.Offset));
            }
        }
    }
}
