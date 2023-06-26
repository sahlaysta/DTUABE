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
    public static class UabeAssetsEditor
    {
        
        public static void UnpackUabeAssets(
            string uabeAssetDumpDir, string stringsDir, string imagesDir, string fontsDir)
        {
            if (uabeAssetDumpDir == null || stringsDir == null || imagesDir == null || fontsDir == null)
                throw new ArgumentException("Null");

            if (!Directory.Exists(uabeAssetDumpDir))
                throw new DirectoryNotFoundException(uabeAssetDumpDir);
            if (!Directory.Exists(stringsDir))
                throw new DirectoryNotFoundException(stringsDir);
            if (!Directory.Exists(imagesDir))
                throw new DirectoryNotFoundException(imagesDir);
            if (!Directory.Exists(fontsDir))
                throw new DirectoryNotFoundException(fontsDir);

            string imagesOgDir = Path.Combine(imagesDir, "originalimages");
            string imagesModDir = Path.Combine(imagesDir, "modifiedimages");
            Directory.CreateDirectory(imagesOgDir);
            Directory.CreateDirectory(imagesModDir);

            string fontsOgDir = Path.Combine(fontsDir, "originalfonts");
            string fontsModDir = Path.Combine(fontsDir, "modifiedfonts");
            Directory.CreateDirectory(fontsOgDir);
            Directory.CreateDirectory(fontsModDir);

            string[] uabeAssetFilePaths = Directory.GetFiles(uabeAssetDumpDir);
            var uabeStringJsonObjects = new List<DTUABEProjectJsonUtil.UabeStringJsonObject>();
            int i = 0;
            foreach (string uabeAssetFilePath in uabeAssetFilePaths)
            {
                i++;
                Console.WriteLine("Reading asset " + i + " of " + uabeAssetFilePaths.Length);

                if (uabeAssetFilePath.EndsWith(".png"))
                {
                    File.Copy(
                        uabeAssetFilePath,
                        Path.Combine(imagesOgDir, Path.GetFileName(uabeAssetFilePath)),
                        true);
                }
                else if (uabeAssetFilePath.EndsWith(".txt"))
                {
                    byte[] fontData = null;

                    using (var uor = new UabeObjectReader(uabeAssetFilePath))
                    {
                        UabeObjectStringData uosd;
                        UabeObjectFontData uofd;
                        while (!uor.Ended)
                        {
                            uor.TryReadNextObjectData(out uosd, out uofd);
                            if (uosd != null)
                            {
                                uabeStringJsonObjects.Add(
                                    new DTUABEProjectJsonUtil.UabeStringJsonObject(
                                        Path.GetFileName(uabeAssetFilePath),
                                        uosd.Line, uosd.Name, uosd.Value, uosd.Value));
                            }
                            if (uofd != null)
                            {
                                fontData = uofd.FontData;
                            }
                        }
                    }

                    if (fontData != null)
                    {
                        File.WriteAllBytes(
                            Path.Combine(fontsOgDir, Path.GetFileName(uabeAssetFilePath) + ".ttf"),
                            fontData);
                    }
                }
                else
                {
                    throw new ArgumentException("Unknown asset type: " + uabeAssetFilePath);
                }
            }
            DTUABEProjectJsonUtil.WriteUabeStringGroupsJsonDir(
                stringsDir,
                new IEnumerable<DTUABEProjectJsonUtil.UabeStringJsonObject>[] { uabeStringJsonObjects });
        }

        public static void RepackUabeAssets(
            string uabeAssetDumpDir, string uabeModAssetsDir,
            string stringsDir, string imagesDir, string fontsDir)
        {
            if (uabeAssetDumpDir == null || uabeModAssetsDir == null
                || stringsDir == null || imagesDir == null || fontsDir == null)
                throw new ArgumentException("Null");

            if (!Directory.Exists(uabeAssetDumpDir))
                throw new DirectoryNotFoundException(uabeAssetDumpDir);
            if (!Directory.Exists(uabeModAssetsDir))
                throw new DirectoryNotFoundException(uabeModAssetsDir);
            if (!Directory.Exists(stringsDir))
                throw new DirectoryNotFoundException(stringsDir);
            if (!Directory.Exists(imagesDir))
                throw new DirectoryNotFoundException(imagesDir);
            if (!Directory.Exists(fontsDir))
                throw new DirectoryNotFoundException(fontsDir);

            foreach (string filePath in Directory.EnumerateFiles(uabeModAssetsDir))
            {
                string originalAssetFilePath = Path.Combine(uabeAssetDumpDir, Path.GetFileName(filePath));
                if (File.Exists(originalAssetFilePath))
                    File.Copy(originalAssetFilePath, filePath, true);
            }

            string imagesModDir = Path.Combine(imagesDir, "modifiedimages");
            if (!Directory.Exists(imagesModDir))
                throw new DirectoryNotFoundException(imagesModDir);

            string fontsModDir = Path.Combine(fontsDir, "modifiedfonts");
            if (!Directory.Exists(fontsModDir))
                throw new DirectoryNotFoundException(fontsModDir);

            IEnumerable<IGrouping<string, DTUABEProjectJsonUtil.UabeStringJsonObject>> fileGroups =
                DTUABEProjectJsonUtil.ReadUabeStringGroupsJsonDir(stringsDir)
                .Where(x => x.OriginalText != x.ModifiedText)
                .GroupBy(x => x.File);

            foreach (IGrouping<string, DTUABEProjectJsonUtil.UabeStringJsonObject> grouping in fileGroups)
            {
                string fileName = grouping.Key;
                string filePath = Path.Combine(uabeAssetDumpDir, fileName);
                string[] lines = File.ReadAllLines(filePath);
                foreach (DTUABEProjectJsonUtil.UabeStringJsonObject uabeStringJsonObject in grouping)
                {
                    int nLine = Convert.ToInt32(uabeStringJsonObject.Line) - 1;
                    string line = lines[nLine];
                    int nLeadingSpaces = line.IndexOf(line.TrimStart(' '));
                    string leadingSpaces = new string(' ', nLeadingSpaces);
                    lines[nLine] = leadingSpaces + "1 string " + uabeStringJsonObject.Name
                        + " = " + toUabeStringLiteral(uabeStringJsonObject.ModifiedText);
                }
                string modAssetFileName = Path.Combine(uabeModAssetsDir, fileName);
                File.WriteAllLines(modAssetFileName, lines);
            }

            foreach (string filePath in Directory.EnumerateFiles(imagesModDir))
            {
                string assetFilePath = Path.Combine(uabeAssetDumpDir, Path.GetFileName(filePath));
                if (File.Exists(assetFilePath))
                {
                    File.Copy(
                        filePath,
                        Path.Combine(uabeModAssetsDir, Path.GetFileName(filePath)),
                        true);
                }
            }

            foreach (string filePath in Directory.EnumerateFiles(fontsModDir))
            {
                if (filePath.EndsWith(".ttf"))
                {
                    string assetFilePath = Path.Combine(
                        uabeAssetDumpDir, Path.GetFileNameWithoutExtension(filePath));
                    string modAssetFilePath = Path.Combine(
                        uabeModAssetsDir, Path.GetFileNameWithoutExtension(filePath));
                    if (File.Exists(assetFilePath))
                    {
                        if (!File.Exists(modAssetFilePath))
                            File.Copy(assetFilePath, modAssetFilePath);

                        List<string> lines = File.ReadAllLines(modAssetFilePath).ToList();
                        byte[] fontData = File.ReadAllBytes(filePath);
                        int vectorIndex = lines.FindIndex(
                            x => x.Trim() == "0 vector m_FontData" || x.Trim() == "1 vector m_FontData");
                        int vectorNum = int.Parse(char.ToString(lines[vectorIndex].Trim()[0]));
                        int arrayNum = int.Parse(char.ToString(lines[vectorIndex + 1].Trim()[0]));
                        int leadingSpaces = lines[vectorIndex].IndexOf(lines[vectorIndex].TrimStart(' '));
                        int vectorSize = int.Parse(lines[vectorIndex + 2].Trim().Substring(13));
                        lines.RemoveRange(vectorIndex, (vectorSize * 2) + 3);
                        int insertIndex = vectorIndex;
                        lines.Insert(insertIndex++, new string(' ', leadingSpaces + 0)
                            + vectorNum + " vector m_FontData");
                        lines.Insert(insertIndex++, new string(' ', leadingSpaces + 1)
                            + arrayNum + " Array Array (" + fontData.Length + " items)");
                        lines.Insert(insertIndex++, new string(' ', leadingSpaces + 2)
                            + "0 int size = " + fontData.Length);
                        for (int i = 0; i < fontData.Length; i++)
                        {
                            byte byteValue = fontData[i];
                            sbyte sbyteValue = (sbyte)byteValue;
                            int intValue = (int)sbyteValue;
                            lines.Insert(insertIndex++, new string(' ', leadingSpaces + 2)
                                + "[" + i + "]");
                            lines.Insert(insertIndex++, new string(' ', leadingSpaces + 3)
                                + "0 char data = " + intValue);
                        }

                        File.WriteAllLines(modAssetFilePath, lines);
                    }
                }
            }
        }

        private class UabeObjectStringData
        {

            private readonly ulong line;
            private readonly string name;
            private readonly string value;

            public UabeObjectStringData(ulong line, string name, string value)
            {
                this.line = line;
                this.name = name;
                this.value = value;
            }

            public ulong Line => line;
            public string Name => name;
            public string Value => value;

        }

        private class UabeObjectFontData
        {

            private readonly byte[] fontData;

            public UabeObjectFontData(byte[] fontData)
            {
                this.fontData = fontData;
            }

            public byte[] FontData => fontData;

        }

        private class UabeObjectReader : IDisposable
        {

            private readonly string uabeAssetFilePath;
            private Func<string> nextLineFunc;
            private Action disposeFunc;
            private bool disposed = false;
            private ulong lineCount = 0;
            private string currentLine;
            private bool ended = false;

            public UabeObjectReader(string uabeAssetFilePath)
            {
                this.uabeAssetFilePath = uabeAssetFilePath;
                var fs = File.Open(uabeAssetFilePath, FileMode.Open, FileAccess.Read);
                var sr = new StreamReader(fs, new UTF8Encoding(false));
                nextLineFunc = () => sr.ReadLine();
                disposeFunc = () => { sr.Dispose(); fs.Dispose(); };
            }

            public bool Ended => ended;

            public void TryReadNextObjectData(out UabeObjectStringData uosd, out UabeObjectFontData uofd)
            {
                uosd = null;
                uofd = null;
                if (!readNextLine())
                {
                    ended = true;
                    return;
                }
                lineCount++;
                tryGetUabeObjectStringData(ref uosd);
                tryGetUabeObjectFontData(ref uofd);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                disposeFunc.Invoke();
                disposeFunc = null;
                nextLineFunc = null;
            }

            private bool readNextLine()
            {
                if (disposed) throw new ObjectDisposedException(GetType().FullName);
                if (ended) throw new InvalidOperationException("Reader ended");
                currentLine = nextLineFunc();
                return currentLine != null;
            }

            private void tryGetUabeObjectStringData(ref UabeObjectStringData uosd)
            {
                try
                {
                    string trimmedLine = currentLine.Trim();
                    if (isFullLengthRegexMatch(trimmedLine, UabeStringDeclarationRegexPattern))
                    {
                        string name;
                        string value;
                        readStringFromUabeStringDeclaration(trimmedLine, out name, out value);
                        uosd = new UabeObjectStringData(lineCount, name, value);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to read UABE asset string: " + currentLine, e);
                }
            }

            private void tryGetUabeObjectFontData(ref UabeObjectFontData uosd)
            {
                try
                {
                    if (currentLine.Trim() == "0 vector m_FontData"
                        || currentLine.Trim() == "1 vector m_FontData")
                    {
                        nextLineValidate(true, "(0|1) Array Array \\([0-9]{1,} items\\)");
                        string arraySizeLine = nextLineValidate(true, "0 int size = [0-9]{1,}");
                        int arraySize = int.Parse(arraySizeLine.Substring(13));
                        var fontData = new byte[arraySize];
                        for (int i = 0; i < arraySize; i++)
                        {
                            nextLineValidate(true, "\\[[0-9]{1,}\\]");
                            string byteValueLine = nextLineValidate(true, "0 char data = ((\\-)|())[0-9]{1,}");
                            sbyte sbyteValue = sbyte.Parse(byteValueLine.Substring(14));
                            byte byteValue = (byte)sbyteValue;
                            fontData[i] = byteValue;
                        }
                        uosd = new UabeObjectFontData(fontData);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to read UABE font data: " + uabeAssetFilePath, e);
                }
            }

            private string nextLineValidate(bool trim, string regex)
            {
                readNextLine();
                string str = trim ? currentLine.Trim() : currentLine;
                if (!isFullLengthRegexMatch(str, regex)) throw new ArgumentException();
                return str;
            }

            private static bool isFullLengthRegexMatch(string str, string regex)
            {
                var match = Regex.Match(str, regex);
                return match.Success && match.Index == 0 && match.Length == str.Length;
            }

        }

        /*
         * The regex expression to match UABE asset string declarations.
         * 
         * Example:
         * 
         * 1 string m_Name = "a string"
         * 
         */
        private static readonly string UabeStringDeclarationRegexPattern =
            "\\s*(1)\\s*(string)\\s*(.*\\s)\\s*(= )(\".*\")";

        private static void readStringFromUabeStringDeclaration(
            string uabeStringDeclaration, out string name, out string value)
        {
            if (uabeStringDeclaration == null || uabeStringDeclaration.Length == 0)
                throw new ArgumentException();
            if (!uabeStringDeclaration.StartsWith("1 string ") || !uabeStringDeclaration.EndsWith("\""))
                throw new ArgumentException();

            name = uabeStringDeclaration.Split()[2];

            string uabeStringLiteral = uabeStringDeclaration.Substring(uabeStringDeclaration.IndexOf('"'));
            value = readUabeStringLiteral(uabeStringLiteral);
        }

        private static string readUabeStringLiteral(string uabeStringLiteral)
        {
            if (uabeStringLiteral == null || uabeStringLiteral.Length == 0)
                throw new ArgumentException();
            if (!uabeStringLiteral.StartsWith("\"") || !uabeStringLiteral.EndsWith("\""))
                throw new ArgumentException();
            if (uabeStringLiteral.EndsWith("\\\""))
                throw new ArgumentException();

            var sb = new StringBuilder();
            for (int i = 1; i < uabeStringLiteral.Length - 1; i++)
            {
                char ch = uabeStringLiteral[i];
                if (ch == '\\')
                {
                    i++;
                    char escapedChar;
                    switch (uabeStringLiteral[i])
                    {
                        case 'n': escapedChar = '\n'; break;
                        case 'r': escapedChar = '\r'; break;
                        case 't': escapedChar = '\t'; break;
                        case '\\': escapedChar = '\\'; break;
                        default: throw new ArgumentException("Unknown escape sequence");
                    }
                    sb.Append(escapedChar);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private static string toUabeStringLiteral(string str)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char ch in str)
            {
                switch (ch)
                {
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\\': sb.Append("\\\\"); break;
                    default: sb.Append(ch); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

    }
}
