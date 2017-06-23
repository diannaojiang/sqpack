using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Sqpack {
    public static class Utils {
        public static string ToHex(this byte[] bytes) {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLower();
        }

        public static short ToInt16(this byte[] bytes) {
            return BitConverter.ToInt16(bytes, 0);
        }

        public static int ToInt32(this byte[] bytes) {
            return BitConverter.ToInt32(bytes, 0);
        }

        public static string ToSha1(this byte[] bytes) {
            var sha1 = SHA1.Create();
            return sha1.ComputeHash(bytes).ToHex();
        }

        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences) {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] {
                Enumerable.Empty<T>()
            };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] {
                        item
                    }));
        }
    }
}
