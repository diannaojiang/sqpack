using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Sqpack.CLI {
    public class Program {
        private static readonly string ConfigFile = Path.Combine(AppContext.BaseDirectory, "config.json");
        private static readonly string OSKey = new[] {OSPlatform.Windows, OSPlatform.Linux, OSPlatform.Windows}.First(RuntimeInformation.IsOSPlatform).ToString();
        private static readonly string GameDirKey = "gameDir_" + OSKey;
        private static Dictionary<string, string> config;

        private static void LoadConfig() {
            if(File.Exists(ConfigFile)) {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>), new DataContractJsonSerializerSettings {
                    UseSimpleDictionaryFormat = true
                });
                using(var stream = File.OpenRead(ConfigFile))
                    config = (Dictionary<string, string>)serializer.ReadObject(stream);
            } else
                config = new Dictionary<string, string>();
        }

        private static void SaveConfig() {
            if(config == null)
                return;

            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>), new DataContractJsonSerializerSettings {
                UseSimpleDictionaryFormat = true
            });
            using(var stream = File.OpenWrite(ConfigFile))
                serializer.WriteObject(stream, config);
        }

        private static Dictionary<uint, Dictionary<uint, int>> GetIndexFolders(string indexFile) {
            var cachePath = Path.Combine(AppContext.BaseDirectory, "cache");
            var indexName = Path.GetFileName(indexFile).Substring(0, Path.GetFileName(indexFile).IndexOf(".", StringComparison.Ordinal));
            var hashFile = Path.Combine(cachePath, indexName + ".hash");
            var cacheFile = Path.Combine(cachePath, indexName + ".json");

            var indexFileHash = SHA1.Create().ComputeHash(File.ReadAllBytes(indexFile)).ToHex();
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<uint, Dictionary<uint, int>>), new DataContractJsonSerializerSettings {
                UseSimpleDictionaryFormat = true
            });
            if(File.Exists(hashFile) && File.ReadAllText(hashFile) == indexFileHash) {
                using(var stream = File.OpenRead(cacheFile))
                    return (Dictionary<uint, Dictionary<uint, int>>)serializer.ReadObject(stream);
            }

            Console.WriteLine("Parsing \"{0}\", this may take a while...", indexFile);
            if(!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);
            File.WriteAllText(hashFile, indexFileHash);
            using(var indexStream = File.OpenRead(indexFile)) {
                var folders = (new IndexParser(indexStream)).GetFolders();
                using(var stream = File.OpenWrite(cacheFile))
                    serializer.WriteObject(stream, folders);
                return folders;
            }
        }

        private static Dictionary<string, Dictionary<uint, Dictionary<uint, int>>> GetFolders(string indexName) {
            var sqpackDir = Path.Combine(config[GameDirKey], "game", "sqpack");
            if(!Directory.Exists(sqpackDir))
                throw new Exception(string.Format("Sqpack directory does not exist. ({0})", sqpackDir));

            var result = new Dictionary<string, Dictionary<uint, Dictionary<uint, int>>>();
            if(indexName == null)
                foreach(var indexFile in Directory.EnumerateFiles(sqpackDir, "*.index", SearchOption.AllDirectories).ToArray()) {
                    indexName = Path.GetFileName(indexFile).Substring(0, Path.GetFileName(indexFile).IndexOf(".", StringComparison.Ordinal));
                    result.Add(indexFile, GetIndexFolders(indexFile));
                }
            else {
                var indexFile = Directory.EnumerateFiles(sqpackDir, "*.index", SearchOption.AllDirectories)
                                         .SingleOrDefault(fullName => Path.GetFileName(fullName).StartsWith(indexName + "."));
                if(indexFile == null)
                    throw new Exception(string.Format("Can not find index file of \"{0}\".", indexName));
                result.Add(indexFile, GetIndexFolders(indexFile));
            }
            return result;
        }

        private static int? SearchFile(IReadOnlyDictionary<uint, Dictionary<uint, int>> folderData, uint folderHash, uint fileHash) {
            if(!folderData.TryGetValue(folderHash, out var folder))
                return null;
            if(!folder.TryGetValue(fileHash, out var file))
                return null;
            return file;
        }

        public static void Main(string[] args) {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            LoadConfig();

            try {
                var argValues = new List<string>(2);
                var argOptions = new Dictionary<string, string>(2);
                for(var i = 0; i < args.Length; i++) {
                    var arg = args[i];
                    if(arg.StartsWith("--"))
                        argOptions.Add(arg.Substring(2), args[++i]);
                    else
                        argValues.Add(arg);
                }

                var fileName = argValues.FirstOrDefault();
                if(fileName == null) {
                    Console.Error.WriteLine("Usage: dotnet Sqpack.CLI.dll <extract_file_name> [--game <game_dir>] [--output <output_path>]");
                    return;
                }
                string indexName = null;
                if(fileName.Contains(":")) {
                    var index = fileName.IndexOf(":", StringComparison.Ordinal);
                    indexName = fileName.Substring(0, index);
                    fileName = fileName.Substring(index + 1);
                }

                if(argOptions.TryGetValue("game", out var gameDir))
                    config[GameDirKey] = gameDir;
                else
                    gameDir = config.ContainsKey(GameDirKey) ? config[GameDirKey] : null;
                if(gameDir == null) {
                    Console.Error.WriteLine("Please specify game location using --game option.");
                    return;
                }

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if(argOptions.TryGetValue("output", out var output))
                    output = Path.GetFullPath(output);
                else
                    output = Directory.GetCurrentDirectory();

                const string sep = "/";
                var fileParts = fileName.Split(new[] {sep}, StringSplitOptions.RemoveEmptyEntries);
                var fileHash = FFCrc.Compute(fileParts.Last());
                var folderHash = FFCrc.Compute(string.Join(sep, fileParts.Take(fileParts.Length - 1)));
                var outputPath = Path.Combine((new[] {output}).Concat(fileParts.Take(fileParts.Length - 1)).ToArray());
                var outputFile = Path.Combine(outputPath, fileParts.Last());
                var offset = 0;

                string datFile = null;
                foreach(var kv in GetFolders(indexName)) {
                    var result = SearchFile(kv.Value, folderHash, fileHash);
                    if(result == null)
                        continue;
                    offset = result.Value;
                    datFile = Path.ChangeExtension(kv.Key, string.Format(".dat{0}", (offset & 0xF) % 2));
                    break;
                }
                if(datFile == null) {
                    Console.Error.WriteLine("Can not find file \"{0}\".", fileName);
                    return;
                }

                using(var stream = File.OpenRead(datFile)) {
                    var data = new DatParser(stream).GetFileData(offset);
                    if(!Directory.Exists(outputPath))
                        Directory.CreateDirectory(outputPath);
                    File.WriteAllBytes(Path.Combine(outputFile), data);
                    Console.WriteLine(outputFile);
                }
            } finally {
                SaveConfig();
            }
        }
    }
}
