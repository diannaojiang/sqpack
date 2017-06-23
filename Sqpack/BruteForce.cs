using System;
using System.Linq;

namespace Sqpack {
    public static class BruteForce {
        private static readonly char[] CharactersToTest = {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z', '1', '2', '3', '4',
            '5', '6', '7', '8', '9', '0', '_'
            // '/', '_', '-', '.'
        };

        private static string Find(int length, uint target, string prefix, string suffix) {
            return Enumerable.Repeat(CharactersToTest, length)
                             .CartesianProduct()
                             .Select(chars => prefix == null && suffix == null
                                 ? new string(chars.ToArray())
                                 : string.Join(string.Empty, prefix, new string(chars.ToArray()), suffix))
                             .FirstOrDefault(str => target == FFCrc.Compute(str));
        }

        public static string Guess(uint target, int maxLength = 6, string prefix = null, string suffix = null) {
            string result = null;
            for(var length = 1; length <= maxLength; length++) {
                Console.WriteLine("Trying length {0}/{1}...", length, maxLength);
                result = Find(length, target, prefix, suffix);
                if(result != null)
                    break;
            }
            return result;
        }
    }
}
