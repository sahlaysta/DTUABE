using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sahlaysta.DTUABE
{
    public static class IlPatcher
    {

        /*
         * 
         * Patch number literal compatibility with float/double value 'inf' in ildasm/ilasm
         * 
         */

        private static readonly (string, string)[] regexReplacementTable = new (string, string)[]
        {
            ( "(ldc.r4)(\\s*)(inf)",  "$1$2(00 00 80 7F )"             ),
            ( "(ldc.r4)(\\s*)(-inf)", "$1$2(00 00 80 FF )"             ),
            ( "(ldc.r8)(\\s*)(inf)",  "$1$2(00 00 00 00 00 00 F0 7F )" ),
            ( "(ldc.r8)(\\s*)(-inf)", "$1$2(00 00 00 00 00 00 F0 FF )" )
        };

        public static void PatchIlFile(string ilFilePath)
        {
            if (ilFilePath == null)
                throw new ArgumentException("Null file path");

            string[] ilFileLines = File.ReadAllLines(ilFilePath);

            for (int i = 0; i < ilFileLines.Length; i++)
            {
                string line = ilFileLines[i];
                foreach ((string regex, string replacement) in regexReplacementTable)
                {
                    var matches = Regex.Matches(line, regex);
                    if (matches.Count == 1 && line.EndsWith(matches[0].Value))
                    {
                        ilFileLines[i] = Regex.Replace(line, regex, replacement);
                        break;
                    }
                }
            }

            File.WriteAllLines(ilFilePath, ilFileLines);
        }

    }
}
