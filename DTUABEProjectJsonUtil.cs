using Newtonsoft.Json;
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
    public static class DTUABEProjectJsonUtil
    {

        internal class IlStringJsonObject
        {

            private readonly string originalText;
            private readonly string modifiedText;

            public IlStringJsonObject(string originalText, string modifiedText)
            {
                this.originalText = originalText;
                this.modifiedText = modifiedText;
            }

            public string OriginalText => originalText;
            public string ModifiedText => modifiedText;

        }

        internal class UabeStringJsonObject
        {

            private readonly string file;
            private readonly ulong line;
            private readonly string name;
            private readonly string originalText;
            private readonly string modifiedText;

            public UabeStringJsonObject(
                string file,
                ulong line,
                string name,
                string originalText,
                string modifiedText)
            {
                this.file = file;
                this.line = line;
                this.name = name;
                this.originalText = originalText;
                this.modifiedText = modifiedText;
            }

            public string File => file;
            public ulong Line => line;
            public string Name => name;
            public string OriginalText => originalText;
            public string ModifiedText => modifiedText;

        }

        internal static IlStringJsonObject[] ReadIlStringsJsonFile(string ilStringsJsonFilePath)
        {
            return readJsonArrayFromFile<IlStringJsonObject>(ilStringsJsonFilePath).ToArray();
        }

        internal static UabeStringJsonObject[] ReadUabeStringGroupsJsonDir(string uabeStringGroupsJsonDir)
        {
            string[] filePaths = getOrderedUabeStringGroupsJsonFilePaths(uabeStringGroupsJsonDir);
            if (filePaths == null || filePaths.Length == 0)
                return new UabeStringJsonObject[0];
            var uabeStringJsonObjects = new List<UabeStringJsonObject>();
            foreach (string filePath in filePaths)
            {
                readJsonArrayFromFile<UabeStringJsonObject>(filePath).ForEach(uabeStringJsonObjects.Add);
            }
            return uabeStringJsonObjects.ToArray();
        }

        internal static void WriteIlStringsJsonFile(
            string ilStringsJsonFilePath, IEnumerable<IlStringJsonObject> ilStrings)
        {
            writeJsonArrayToFile(ilStringsJsonFilePath, ilStrings);
        }

        internal static void WriteUabeStringGroupsJsonDir(
            string uabeStringGroupsJsonDir,
            IEnumerable<IEnumerable<UabeStringJsonObject>> uabeStringGroups)
        {
            if (!Directory.Exists(uabeStringGroupsJsonDir))
                throw new DirectoryNotFoundException(uabeStringGroupsJsonDir);

            moveUabeStringGroupsJsonFilesToBackup(uabeStringGroupsJsonDir);

            ulong i = 0;
            foreach (IEnumerable<UabeStringJsonObject> uabeStringGroup in uabeStringGroups)
            {
                string fileName = "uabestrings.group" + i + ".json";
                string filePath = Path.Combine(uabeStringGroupsJsonDir, fileName);
                var list = uabeStringGroup.ToList();
                sortUabeStringJsonObjects(list);
                writeJsonArrayToFile(filePath, list);
                i++;
            }
        }

        internal static void RegroupUabeJsonStringsByName(
            string uabeStringGroupsJsonDir, IEnumerable<ISet<string>> nameGroups)
        {
            if (!Directory.Exists(uabeStringGroupsJsonDir))
                throw new DirectoryNotFoundException(uabeStringGroupsJsonDir);

            List<UabeStringJsonObject> uabeStringJsonObjects
                = ReadUabeStringGroupsJsonDir(uabeStringGroupsJsonDir).ToList();

            var groups = new List<List<UabeStringJsonObject>>();
            foreach (ISet<string> nameGroup in nameGroups)
            {
                var list = new List<UabeStringJsonObject>();
                foreach (UabeStringJsonObject uabeStringJsonObject in uabeStringJsonObjects)
                {
                    if (nameGroup.Contains(uabeStringJsonObject.Name))
                    {
                        list.Add(uabeStringJsonObject);
                    }
                }
                foreach (UabeStringJsonObject uabeStringJsonObject in list)
                {
                    uabeStringJsonObjects.Remove(uabeStringJsonObject);
                }
                groups.Add(list);
            }
            if (uabeStringJsonObjects.Count > 0)
                groups.Add(uabeStringJsonObjects);

            WriteUabeStringGroupsJsonDir(uabeStringGroupsJsonDir, groups);
        }

        private static void moveUabeStringGroupsJsonFilesToBackup(string uabeStringGroupsJsonDir)
        {
            string[] filePaths = getOrderedUabeStringGroupsJsonFilePaths(uabeStringGroupsJsonDir);
            if (filePaths == null || filePaths.Length == 0)
                return;
            string backupDirName = "uabestringsbackup" + DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string backupDir = Path.Combine(uabeStringGroupsJsonDir, backupDirName);
            if (Directory.Exists(backupDir))
                throw new ArgumentException("Directory existed: " + backupDir);
            Directory.CreateDirectory(backupDir);
            foreach (string filePath in filePaths)
            {
                File.Move(filePath, Path.Combine(backupDir, Path.GetFileName(filePath)));
            }
        }

        private static string[] getOrderedUabeStringGroupsJsonFilePaths(string uabeStringGroupsJsonDir)
        {
            var found = new List<(string filePath, ulong id)>();
            foreach (string filePath in Directory.EnumerateFiles(uabeStringGroupsJsonDir))
            {
                string fileName = Path.GetFileName(filePath);
                if (Regex.Matches(fileName, "^uabestrings\\.group[0-9]{1,}\\.json$").Count == 1)
                {
                    string numericPart = Regex.Matches(fileName, "[0-9]{1,}")[0].Value;
                    ulong id = ulong.Parse(numericPart);
                    found.Add((filePath, id));
                }
            }
            found.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return found.Select(x => x.Item1).ToArray();
        }

        private static void writeJsonArrayToFile<T>(string filePath, IEnumerable<T> items)
        {
            var serializer = new JsonSerializer();
            serializer.MaxDepth = 2;
            serializer.TypeNameHandling = TypeNameHandling.None;
            using (var fs = File.Open(filePath, FileMode.Create, FileAccess.Write))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
            using (var jtw = new JsonTextWriter(sw))
            {
                jtw.Formatting = Formatting.Indented;
                jtw.IndentChar = ' ';
                jtw.Indentation = 4;
                serializer.Serialize(jtw, items);
            }
        }

        private static List<T> readJsonArrayFromFile<T>(string filePath)
        {
            var serializer = new JsonSerializer();
            serializer.MaxDepth = 2;
            serializer.TypeNameHandling = TypeNameHandling.None;
            using (var fs = File.Open(filePath, FileMode.Open))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
            using (var jtr = new JsonTextReader(sr))
            {
                return serializer.Deserialize<List<T>>(jtr);
            }
        }

        private static void sortUabeStringJsonObjects(List<UabeStringJsonObject> uabeStringJsonObjects)
        {
            uabeStringJsonObjects.Sort(compareUabeStringJsonObjects);
        }

        private static int compareUabeStringJsonObjects(
            UabeStringJsonObject uabeStringJsonObjectA,
            UabeStringJsonObject uabeStringJsonObjectB)
        {
            string assetFileNameA = uabeStringJsonObjectA.File;
            string assetFileNameB = uabeStringJsonObjectB.File;
            string containerAssetFileNameA;
            string containerAssetFileNameB;
            ulong assetIdA;
            ulong assetIdB;
            ulong lineA = uabeStringJsonObjectA.Line;
            ulong lineB = uabeStringJsonObjectB.Line;
            parseAssetFileName(assetFileNameA, out containerAssetFileNameA, out assetIdA);
            parseAssetFileName(assetFileNameB, out containerAssetFileNameB, out assetIdB);
            if (containerAssetFileNameA.StartsWith("level")
                && containerAssetFileNameB.StartsWith("level"))
            {
                int strlen = "level".Length;
                string numericPartA = containerAssetFileNameA.Substring(strlen);
                string numericPartB = containerAssetFileNameB.Substring(strlen);
                if (numericPartA.All(char.IsDigit) && numericPartB.All(char.IsDigit))
                {
                    ulong numericValueA = ulong.Parse(numericPartA);
                    ulong numericValueB = ulong.Parse(numericPartB);
                    int compare = numericValueA.CompareTo(numericValueB);
                    if (compare == 0)
                    {
                        int assetIdCompare = assetIdA.CompareTo(assetIdB);
                        return assetIdCompare == 0 ? lineA.CompareTo(lineB) : assetIdCompare;
                    }
                    else
                    {
                        return compare;
                    }
                }
            }
            if (containerAssetFileNameA.StartsWith("sharedassets")
                && containerAssetFileNameA.Contains('.')
                && containerAssetFileNameB.StartsWith("sharedassets")
                && containerAssetFileNameB.Contains('.'))
            {
                int strlen = "sharedassets".Length;
                string numericPartA = containerAssetFileNameA.Substring(
                    strlen, containerAssetFileNameA.IndexOf('.') - strlen);
                string numericPartB = containerAssetFileNameB.Substring(
                    strlen, containerAssetFileNameB.IndexOf('.') - strlen);
                if (numericPartA.All(char.IsDigit) && numericPartB.All(char.IsDigit))
                {
                    ulong numericValueA = ulong.Parse(numericPartA);
                    ulong numericValueB = ulong.Parse(numericPartB);
                    int compare = numericValueA.CompareTo(numericValueB);
                    if (compare == 0)
                    {
                        int assetIdCompare = assetIdA.CompareTo(assetIdB);
                        return assetIdCompare == 0 ? lineA.CompareTo(lineB) : assetIdCompare;
                    }
                    else
                    {
                        return compare;
                    }
                }
            }
            int strCompare = String.Compare(containerAssetFileNameA, containerAssetFileNameB);
            if (strCompare == 0)
            {
                int assetIdCompare = assetIdA.CompareTo(assetIdB);
                return assetIdCompare == 0 ? lineA.CompareTo(lineB) : assetIdCompare;
            }
            else
            {
                return strCompare;
            }
        }

        private static void parseAssetFileName(
            string assetFileName, out string containerAssetFileName, out ulong assetId)
        {
            string[] splits = Path.GetFileNameWithoutExtension(assetFileName).Split('-');
            if (splits.Length != 3 || !splits[2].All(char.IsDigit))
                throw new ArgumentException("Invalid asset file path: " + assetFileName);
            containerAssetFileName = splits[1];
            assetId = ulong.Parse(splits[2]);
        }

    }
}
