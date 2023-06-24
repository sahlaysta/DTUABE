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
    public static class IlStringsEditor
    {
        
        public static void UnpackIlStrings(string ilFile, string ilStringsJsonFile)
        {
            if (ilFile == null || ilStringsJsonFile == null)
                throw new ArgumentException("Null");

            var ilStrings = new OrderedSet<string>();
            string ilFileContent = File.ReadAllText(ilFile);
            var match = Regex.Match(ilFileContent, IlStringDeclarationRegexPattern);
            while (match.Success)
            {
                string preLiteral;
                string ilString;
                readStringFromIlStringDeclaration(match.Value, out preLiteral, out ilString);
                ilStrings.Add(ilString);
                match = match.NextMatch();
            }

            var ilStringJsonObjects = ilStrings.Select(x => new DTUABEProjectJsonUtil.IlStringJsonObject(x, x));
            DTUABEProjectJsonUtil.WriteIlStringsJsonFile(ilStringsJsonFile, ilStringJsonObjects);
        }

        public static void RepackIlStrings(string ilFile, string ilStringsJsonFile)
        {
            if (ilFile == null || ilStringsJsonFile == null)
                throw new ArgumentException("Null");

            Dictionary<string, string> stringDict =
                DTUABEProjectJsonUtil.ReadIlStringsJsonFile(ilStringsJsonFile)
                .Where(x => x.OriginalText != x.ModifiedText)
                .ToDictionary(x => x.OriginalText, x => x.ModifiedText);
            string ilFileContent = File.ReadAllText(ilFile);
            var sb = new StringBuilder();
            int i = 0;
            var match = Regex.Match(ilFileContent, IlStringDeclarationRegexPattern);
            while (match.Success)
            {
                string preLiteral;
                string ilString;
                readStringFromIlStringDeclaration(match.Value, out preLiteral, out ilString);
                sb.Append(ilFileContent, i, match.Index - i);
                string newIlString;
                if (stringDict.TryGetValue(ilString, out newIlString))
                    sb.Append(preLiteral + toIlByteArrayStringLiteral(newIlString));
                else
                    sb.Append(match.Value);
                i = match.Index + match.Length;
                match = match.NextMatch();
            }
            sb.Append(ilFileContent, i, ilFileContent.Length - i);
            File.WriteAllText(ilFile, sb.ToString());
        }

        private static string toIlByteArrayStringLiteral(string str)
        {
            var sb = new StringBuilder();
            sb.Append("bytearray (");
            foreach (char c in str)
            {
                string hex = ((int)c).ToString("X4");
                sb.Append(hex.Substring(2, 2));
                sb.Append(' ');
                sb.Append(hex.Substring(0, 2));
                sb.Append(' ');
            }
            sb.Append(')');
            return sb.ToString();
        }

        /*
         * The regex expression to match IL string declarations.
         * 
         * Three example matches:
         * 
         * IL_000f:  ldstr      "a string"
         * 
         * IL_005d:  ldstr      "a concatenated"
         *                      + "string"
         * 
         * IL_0078:  ldstr      bytearray (08 00 20 00 20 00 20 00 20 00 50 00 45 00 52 00   // .. . . . .P.E.R.
         *                                 48 00 41 00 50 00 53 00 20 00 59 00 4F 00 55 00 ) // H.A.P.S. .Y.O.U.
         * 
         */
        private static readonly string IlStringDeclarationRegexPattern =
            "(?<=\n)(\\s*(IL_[0-9A-Fa-f]{4}):\\s*(ldstr)\\s*)((\"(?:\\\\.|[^\\\\\"])*\")((\\s*\\+\\s*\"(?:\\\\.|[^" +
            "\\\\\"])*\"){1,}|())|((bytearray)\\s*\\(\\s*(([0-9A-Fa-f]{2})\\s*((\\/\\/.*\\n\\s*)|())){1,}\\)))";

        private static void readStringFromIlStringDeclaration(
            string ilStringDeclaration, out string preLiteral, out string stringValue)
        {
            var match = Regex.Match(ilStringDeclaration, "\\s*(IL_[0-9A-Fa-f]{4}):\\s*(ldstr)\\s*");
            if (!match.Success || match.Index != 0)
                throw new ArgumentException(ilStringDeclaration);
            preLiteral = match.Value;
            string literal = ilStringDeclaration.Substring(match.Length).Trim();
            try
            {
                if (literal.StartsWith("\""))
                {
                    stringValue = readIlConcatenatedStringLiteral(literal);
                    return;
                }
                else if (literal.StartsWith("b"))
                {
                    stringValue = readIlByteArrayStringLiteral(literal);
                    return;
                }
            }
            catch (Exception e)
            {
                throw new Exception(ilStringDeclaration, e);
            }
            throw new ArgumentException(ilStringDeclaration);
        }

        private static string readIlConcatenatedStringLiteral(string ilConcatenatedStringLiteral)
        {
            if (ilConcatenatedStringLiteral == null || ilConcatenatedStringLiteral.Length == 0)
                throw new ArgumentException();
            if (!ilConcatenatedStringLiteral.StartsWith("\"") || !ilConcatenatedStringLiteral.EndsWith("\""))
                throw new ArgumentException();

            var sb = new StringBuilder();
            using (IEnumerator<char> chars = ilConcatenatedStringLiteral.GetEnumerator())
            {
                var sr = new StringReader(chars);
                sr.ReadNextChar();
                while (true)
                {
                    while (true)
                    {
                        sr.ReadNextChar();
                        if (sr.CurrentChar == '"')
                        {
                            break;
                        }
                        else if (sr.CurrentChar == '\\')
                        {
                            char escapedChar;
                            sr.ReadNextChar();
                            switch (sr.CurrentChar)
                            {
                                case 'n': escapedChar = '\n'; break;
                                case 'r': escapedChar = '\r'; break;
                                case 't': escapedChar = '\t'; break;
                                case 'b': escapedChar = '\b'; break;
                                case '\\': escapedChar = '\\'; break;
                                case '?': escapedChar = '?'; break;
                                case '"': escapedChar = '"'; break;
                                default: throw new ArgumentException("Unknown escape sequence");
                            }
                            sb.Append(escapedChar);
                        }
                        else
                        {
                            sb.Append(sr.CurrentChar);
                        }
                    }
                    sr.ReadNextChar();
                    if (sr.Ended) break;
                    sr.SkipWhiteSpace();
                    sr.RequireCurrentChar('+');
                    sr.ReadNextChar();
                    sr.SkipWhiteSpace();
                    sr.RequireCurrentChar('"');
                }
            }
            return sb.ToString();
        }

        private static string readIlByteArrayStringLiteral(string ilByteArrayStringLiteral)
        {
            if (ilByteArrayStringLiteral == null || ilByteArrayStringLiteral.Length == 0)
                throw new ArgumentException();
            if (!ilByteArrayStringLiteral.StartsWith("bytearray") || !ilByteArrayStringLiteral.EndsWith(")"))
                throw new ArgumentException();

            var sb = new StringBuilder();
            var hex = new char[4];
            bool read4 = false;
            using (IEnumerator<char> chars = ilByteArrayStringLiteral.GetEnumerator())
            {
                var sr = new StringReader(chars);
                sr.ReadNextChar();

                for (int i = 0; i < "bytearray".Length; i++)
                    sr.ReadNextChar();

                sr.SkipWhiteSpace();
                sr.RequireCurrentChar('(');
                sr.ReadNextChar();
                sr.SkipWhiteSpace();

                char hexChar1 = '\0', hexChar2 = '\0', hexChar3 = '\0', hexChar4 = '\0';
                while (true)
                {
                    char ch1 = sr.CurrentChar;
                    sr.ReadNextChar();
                    char ch2 = sr.CurrentChar;

                    if (!read4)
                    {
                        read4 = true;
                        hexChar1 = ch1;
                        hexChar2 = ch2;
                    }
                    else
                    {
                        read4 = false;
                        hexChar3 = ch1;
                        hexChar4 = ch2;
                        hex[0] = hexChar3;
                        hex[1] = hexChar4;
                        hex[2] = hexChar1;
                        hex[3] = hexChar2;
                        int hexAsInt = Convert.ToInt32(new string(hex), 16);
                        sb.Append((char)hexAsInt);
                    }

                    sr.ReadNextChar();
                    sr.RequireCurrentCharWhitespace();
                    sr.SkipWhiteSpace();
                    if (sr.CurrentChar == ')')
                    {
                        break;
                    }
                    else if (sr.CurrentChar == '/')
                    {
                        sr.ReadNextChar();
                        sr.RequireCurrentChar('/');
                        sr.ReadNextChar();
                        while (sr.CurrentChar != '\n')
                            sr.ReadNextChar();
                        sr.SkipWhiteSpace();
                    }
                }

            }

            if (read4)
                throw new ArgumentException("Insufficient byte array");

            return sb.ToString();
        }

        private class StringReader
        {

            private IEnumerator<char> chars;
            private char current;
            private bool started = false;
            private bool ended = false;

            public StringReader(IEnumerator<char> chars) => this.chars = chars;

            public bool Started => started;

            public bool Ended => ended;

            public char CurrentChar => current;

            public void ReadNextChar()
            {
                if (!started) started = true;
                if (ended) throw new InvalidOperationException("Reader ended");
                if (chars.MoveNext())
                {
                    current = chars.Current;
                }
                else
                {
                    current = default(char);
                    ended = true;
                }
            }

            public void SkipWhiteSpace()
            {
                while (char.IsWhiteSpace(CurrentChar))
                {
                    ReadNextChar();
                }
            }

            public void RequireCurrentChar(char ch)
            {
                if (CurrentChar != ch)
                    throw new ArgumentException("Unknown symbol");
            }
            public void RequireCurrentCharWhitespace()
            {
                if (!char.IsWhiteSpace(CurrentChar))
                    throw new ArgumentException("Unknown symbol");
            }

        }
        
        private class OrderedSet<T> : IEnumerable<T>
        {

            private readonly OrderedDictionary dict = new OrderedDictionary();
            private static readonly object nullKey = new object();
            private static readonly object dictValue = new object();

            public OrderedSet() { }

            public void Add(T element) => dict[element == null ? nullKey : element] = dictValue;

            public bool Contains(T element) => dict.Contains(element == null ? nullKey : element);

            public int Count => dict.Count;

            public IEnumerator<T> GetEnumerator() =>
                dict.Keys.Cast<Object>().Select(x => x == nullKey ? default(T) : (T)x).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() =>
                dict.Keys.Cast<Object>().Select(x => x == nullKey ? default(T) : x).GetEnumerator();

        }

    }
}
