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
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var argModel = new Dictionary<string, IDictionary<string, Args.ArgType>>
            {
                {
                    "createproject",
                    new Dictionary<string, Args.ArgType>()
                    {
                        { "--projectdir", Args.ArgType.Value },
                        { "--unitygameexe", Args.ArgType.Value },
                        { "--force", Args.ArgType.Flag }
                    }
                },
                {
                    "reorderproject",
                    new Dictionary<string, Args.ArgType>()
                    {
                        { "--projectdir", Args.ArgType.Value },
                        { "--precedenames", Args.ArgType.Value }
                    }
                },
                {
                    "createmod",
                    new Dictionary<string, Args.ArgType>()
                    {
                        { "--projectdir", Args.ArgType.Value },
                        { "--originalunitygameexe", Args.ArgType.Value },
                        { "--modunitygameexe", Args.ArgType.Value }
                    }
                }
            };
            var args = new Args(
                Environment.GetCommandLineArgs().Skip(1),
                argModel);

            switch (args.GroupKey)
            {
                case "createproject":
                    {
                        string projectdir = args.RequireArgValue("--projectdir");
                        string unitygameexe = args.RequireArgValue("--unitygameexe");
                        bool force = args.HasArgFlag("--force");
                        createProject(projectdir, unitygameexe, force);
                        break;
                    }
                case "reorderproject":
                    {
                        string projectdir = args.RequireArgValue("--projectdir");
                        string precedenames = args.RequireArgValue("--precedenames");
                        reorderProject(projectdir, precedenames);
                        break;
                    }
                case "createmod":
                    {
                        string projectdir = args.RequireArgValue("--projectdir");
                        string originalunitygameexe = args.RequireArgValue("--originalunitygameexe");
                        string modunitygameexe = args.RequireArgValue("--modunitygameexe");
                        createMod(projectdir, originalunitygameexe, modunitygameexe);
                        break;
                    }
            }
        }

        private class Args
        {

            public enum ArgType { Value, Flag }

            private readonly HashSet<string> flags = new HashSet<string>();
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();
            private readonly string argGroupKey;

            public Args(IEnumerable<string> args, IDictionary<string, IDictionary<string, ArgType>> argModel)
            {
                using (IEnumerator<string> argEnumerator = args.GetEnumerator())
                {
                    if (!argEnumerator.MoveNext())
                        throw new ArgumentException("No args");

                    argGroupKey = argEnumerator.Current;
                    IDictionary<string, ArgType> argTypeDict;
                    if (!argModel.TryGetValue(argGroupKey, out argTypeDict))
                        throw new ArgumentException("Unknown arg: " + argGroupKey);

                    while (argEnumerator.MoveNext())
                    {
                        string argName = argEnumerator.Current;
                        ArgType argType;
                        if (!argTypeDict.TryGetValue(argName, out argType))
                            throw new ArgumentException("Invalid arg: " + argName);
                        switch (argType)
                        {
                            case ArgType.Value:
                                if (!argEnumerator.MoveNext())
                                    throw new ArgumentException("Expected an arg");
                                values[argName] = argEnumerator.Current;
                                break;
                            case ArgType.Flag:
                                flags.Add(argName);
                                break;
                        }
                    }
                }
            }

            public string GroupKey => argGroupKey;

            public bool HasArgFlag(string argName) => flags.Contains(argName);

            public string GetArgValue(string argName)
            {
                string result;
                values.TryGetValue(argName, out result);
                return result;
            }

            public string RequireArgValue(string argName)
            {
                string result;
                if (!values.TryGetValue(argName, out result))
                    throw new ArgumentException("Required arg: " + argName);
                return result;
            }

        }

        private static void createProject(string projectDir, string unityGameExeFilePath, bool force)
        {
            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);
            if (!File.Exists(unityGameExeFilePath))
                throw new ArgumentException("File not found: " + unityGameExeFilePath);
            if (!force && Directory.EnumerateFileSystemEntries(projectDir).Any())
                throw new ArgumentException(
                    "Can only create project in empty directory. (Use --force to force)");

            projectDir = Path.GetFullPath(projectDir);
            unityGameExeFilePath = Path.GetFullPath(unityGameExeFilePath);

            string unityGameDataDir;
            IEnumerable<string> unityGameAssetFilePaths;
            string unityGameDllDir;
            getAssetFilesAndDllDir(
                unityGameExeFilePath, out unityGameDataDir, out unityGameAssetFilePaths,
                out unityGameDllDir);

            DTUABEProjectEditor.CreateProject(projectDir, unityGameAssetFilePaths, unityGameDllDir);
        }

        private static void reorderProject(string projectDir, string precedeNames)
        {
            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);

            projectDir = Path.GetFullPath(projectDir);
            
            DTUABEProjectEditor.ReorderProjectUabeStrings(
                projectDir,
                new HashSet<string>(precedeNames.Split('|')));
        }

        private static void createMod(
            string projectDir,
            string originalUnityGameExeFilePath,
            string modUnityGameExeFilePath)
        {
            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);
            if (!File.Exists(originalUnityGameExeFilePath))
                throw new ArgumentException("File not found: " + originalUnityGameExeFilePath);
            if (!File.Exists(modUnityGameExeFilePath))
                throw new ArgumentException("File not found: " + modUnityGameExeFilePath);

            projectDir = Path.GetFullPath(projectDir);
            originalUnityGameExeFilePath = Path.GetFullPath(originalUnityGameExeFilePath);
            modUnityGameExeFilePath = Path.GetFullPath(modUnityGameExeFilePath);

            string originalUnityGameDataDir;
            IEnumerable<string> originalUnityGameAssetFilePaths;
            string originalUnityGameDllDir;
            getAssetFilesAndDllDir(
                originalUnityGameExeFilePath, out originalUnityGameDataDir,
                out originalUnityGameAssetFilePaths, out originalUnityGameDllDir);

            string modUnityGameDataDir;
            IEnumerable<string> modUnityGameAssetFilePaths;
            string modUnityGameDllDir;
            getAssetFilesAndDllDir(
                modUnityGameExeFilePath, out modUnityGameDataDir,
                out modUnityGameAssetFilePaths, out modUnityGameDllDir);

            DTUABEProjectEditor.CreateMod(
                projectDir, originalUnityGameAssetFilePaths, originalUnityGameDllDir,
                modUnityGameDllDir, modUnityGameDataDir);
        }

        private static void getAssetFilesAndDllDir(
            string unityGameExeFilePath,
            out string unityGameDataDir,
            out IEnumerable<string> unityGameAssetFilePaths,
            out string unityGameDllDir)
        {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(unityGameExeFilePath);
            string unityGameDataDirName = fileNameNoExt + "_Data";
            unityGameDataDir = Path.Combine(
                Path.GetDirectoryName(unityGameExeFilePath), unityGameDataDirName);
            if (!Directory.Exists(unityGameDataDir))
                throw new ArgumentException("Directory not found: " + unityGameDataDir);
            unityGameAssetFilePaths = Directory.GetFiles(unityGameDataDir).Where(isUnityAssetFilePath).OrderBy(x => x);

            unityGameDllDir = Path.Combine(unityGameDataDir, "Managed");
            if (!Directory.Exists(unityGameDllDir))
                throw new ArgumentException("Directory not found: " + unityGameDllDir);
        }

        private static bool isUnityAssetFilePath(string filePath)
        {
            if (filePath.EndsWith(".assets") || filePath.EndsWith(".resource") || filePath.EndsWith(".resS"))
                return true;
            string fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("level") && fileName.Substring(5).All(isDigit))
                return true;
            if (fileName.Length == 32 && fileName.All(isAlphanumeric))
                return true;
            if (fileName == "globalgamemanagers")
                return true;
            return false;
        }

        private static bool isDigit(char ch)
        {
            return ch >= '0' && ch <= '9';
        }

        private static bool isAlphanumeric(char ch)
        {
            return (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
        }

    }
}
