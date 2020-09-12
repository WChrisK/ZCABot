using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ZCABot
{
    public static class Util
    {
        /// <summary>
        /// Takes a string to be split that may have quoted stuff and bundles
        /// the quoted stuff together.
        /// </summary>
        /// <remarks>
        /// Example: `hi there "mr person" :)` -> ["hi", "there", "mr person", ":)"]
        /// Source: https://stackoverflow.com/questions/14655023/split-a-string-that-has-white-spaces-unless-they-are-enclosed-within-quotes
        /// </remarks>
        /// <param name="str">The string to split that may have quotes.</param>
        /// <returns>A list of tokens.</returns>
        public static IList<string> SplitQuoted(this string str)
        {
            return Regex.Matches(str, @"[\""].+?[\""]|[^ ]+").Select(m => m.Value.Replace("\"", "")).ToList();
        }
    }
}
