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
    public static class DTUABEProjectEditor
    {

        public static void CreateProject(
            string projectDir, IEnumerable<string> assetFilePaths, string dllDir)
        {
            if (projectDir == null || assetFilePaths == null || dllDir == null)
                throw new ArgumentException("Null");

            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);

            if (!Directory.Exists(dllDir))
                throw new ArgumentException("Directory not found: " + dllDir);

            string ildasmExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IL", "ildasm", "ildasm.exe");
            if (!File.Exists(ildasmExe))
                throw new ArgumentException("File not found: " + ildasmExe);

            string autouabeExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoUABE", "AssetBundleExtractor.exe");
            if (!File.Exists(autouabeExe))
                throw new ArgumentException("File not found: " + autouabeExe);

            string assemblyCSharpDll = Path.Combine(dllDir, "Assembly-CSharp.dll");
            if (!File.Exists(assemblyCSharpDll))
                throw new ArgumentException("DLL not found: Assembly-CSharp.dll");

            if (!assetFilePaths.Any(x => Path.GetFileName(x) == "globalgamemanagers.assets"))
                throw new ArgumentException("Asset not found: globalgamemanagers.assets");

            string ilFile = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "il", "il.il");
            string ilStringsJsonFile = Path.Combine(projectDir, "strings", "ilstrings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ilFile));
            Directory.CreateDirectory(Path.GetDirectoryName(ilStringsJsonFile));
            unpackIlStrings(ildasmExe, assemblyCSharpDll, ilFile, ilStringsJsonFile);

            Console.WriteLine();

            string uabeAssetDumpDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "uabeassetdump");
            string uabeTempDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT");
            string uabeStringsDir = Path.Combine(projectDir, "strings");
            string uabeImagesDir = Path.Combine(projectDir, "images");
            string uabeFontsDir = Path.Combine(projectDir, "fonts");
            Directory.CreateDirectory(uabeAssetDumpDir);
            Directory.CreateDirectory(uabeTempDir);
            Directory.CreateDirectory(uabeStringsDir);
            Directory.CreateDirectory(uabeImagesDir);
            Directory.CreateDirectory(uabeFontsDir);
            unpackUabeAssets(autouabeExe, uabeTempDir, uabeAssetDumpDir,
                assetFilePaths, dllDir, uabeStringsDir, uabeImagesDir, uabeFontsDir);
        }

        public static void ReorderProjectUabeStrings(string projectDir, ISet<string> names)
        {
            if (projectDir == null || names == null)
                throw new ArgumentException("Null");

            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);

            string stringsDir = Path.Combine(projectDir, "strings");
            if (!Directory.Exists(stringsDir))
                throw new ArgumentException("Directory not found: " + stringsDir);

            Console.WriteLine("Regrouping UABE strings");
            DTUABEProjectJsonUtil.RegroupUabeJsonStringsByName(
                stringsDir,
                new ISet<string>[] { names });
        }

        public static void CreateMod(
            string projectDir, IEnumerable<string> originalAssetFilePaths,
            string originalDllDir, string modDllDir, string uabeModGameDataDir)
        {
            if (projectDir == null || originalAssetFilePaths == null
                || modDllDir == null || uabeModGameDataDir == null)
                throw new ArgumentException("Null");

            if (!Directory.Exists(projectDir))
                throw new ArgumentException("Directory not found: " + projectDir);

            if (!Directory.Exists(modDllDir))
                throw new ArgumentException("Directory not found: " + modDllDir);

            if (!Directory.Exists(uabeModGameDataDir))
                throw new ArgumentException("Directory not found: " + uabeModGameDataDir);

            if (!originalAssetFilePaths.Any(x => Path.GetFileName(x) == "globalgamemanagers.assets"))
                throw new ArgumentException("Asset not found: globalgamemanagers.assets");

            string ilasmExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IL", "ilasm", "ilasm.exe");
            if (!File.Exists(ilasmExe))
                throw new ArgumentException("File not found: " + ilasmExe);

            string autouabeExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutoUABE", "AssetBundleExtractor.exe");
            if (!File.Exists(autouabeExe))
                throw new ArgumentException("File not found: " + autouabeExe);

            string uabeAssetDumpDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "uabeassetdump");
            if (!Directory.Exists(uabeAssetDumpDir))
                throw new ArgumentException("Directory not found: " + uabeAssetDumpDir);

            string uabeTempDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT");
            Directory.CreateDirectory(uabeTempDir);

            string uabeStringsDir = Path.Combine(projectDir, "strings");
            if (!Directory.Exists(uabeStringsDir))
                throw new ArgumentException("Directory not found: " + uabeStringsDir);

            string uabeImagesDir = Path.Combine(projectDir, "images");
            if (!Directory.Exists(uabeImagesDir))
                throw new ArgumentException("Directory not found: " + uabeImagesDir);

            string uabeFontsDir = Path.Combine(projectDir, "fonts");
            if (!Directory.Exists(uabeFontsDir))
                throw new ArgumentException("Directory not found: " + uabeFontsDir);

            string uabeAssetsSaveDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "uabesaveassets");
            Directory.CreateDirectory(uabeAssetsSaveDir);

            string uabeModAssetsDir = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "uabemodassets");
            Directory.CreateDirectory(uabeModAssetsDir);

            string modAssemblyCSharpDll = Path.Combine(modDllDir, "Assembly-CSharp.dll");
            if (!File.Exists(modAssemblyCSharpDll))
                throw new ArgumentException("DLL not found: Assembly-CSharp.dll");

            string ilFile = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "il", "il.il");
            if (!File.Exists(ilFile))
                throw new ArgumentException("File not found: " + ilFile);

            string ilFileBackup = Path.Combine(projectDir, "dtuabebin", "DO_NOT_EDIT", "il", "il.il.original");
            if (!File.Exists(ilFileBackup))
                throw new ArgumentException("File not found: " + ilFileBackup);

            string ilStringsJsonFile = Path.Combine(projectDir, "strings", "ilstrings.json");
            if (!File.Exists(ilStringsJsonFile))
                throw new ArgumentException("File not found: " + ilStringsJsonFile);

            File.Copy(ilFileBackup, ilFile, true);

            repackIlStrings(ilasmExe, modAssemblyCSharpDll, ilFile, ilStringsJsonFile);

            Console.WriteLine();

            repackUabeAssets(autouabeExe, uabeTempDir, uabeAssetDumpDir,
                originalAssetFilePaths, originalDllDir, uabeStringsDir,
                uabeImagesDir, uabeFontsDir, uabeAssetsSaveDir,
                uabeModGameDataDir, uabeModAssetsDir);
        }

        private static void unpackIlStrings(
            string ildasmExe, string assemblyCSharpDll, string ilFile, string ilStringsJsonFile)
        {
            string ildasmDir = Path.GetDirectoryName(ildasmExe);
            string[] processArgs = new string[] { assemblyCSharpDll, "/OUT=" + ilFile };

            Console.WriteLine("Running ildasm");
            int processExitCode = ProcessUtil.RunBackgroundProcess(ildasmExe, processArgs, ildasmDir);
            if (processExitCode != 0)
            {
                throw new Exception("ildasm failed, non-zero exit code");
            }
            Console.WriteLine("ildasm done");

            File.Copy(ilFile, ilFile + ".original", true);

            Console.WriteLine();
            Console.WriteLine("Unpacking IL strings");
            IlStringsEditor.UnpackIlStrings(ilFile, ilStringsJsonFile);
        }

        private static void repackIlStrings(
            string ilasmExe, string modAssemblyCSharpDll, string ilFile, string ilStringsJsonFile)
        {
            Console.WriteLine("Repacking IL strings");
            IlStringsEditor.RepackIlStrings(ilFile, ilStringsJsonFile);

            Console.WriteLine();
            Console.WriteLine("Patching IL file");
            IlPatcher.PatchIlFile(ilFile);

            Console.WriteLine();
            Console.WriteLine("Running ilasm");
            string[] processArgs = new string[] { ilFile, "/dll" };
            string ilasmDir = Path.GetDirectoryName(ilasmExe);
            int processExitCode = ProcessUtil.RunBackgroundProcess(ilasmExe, processArgs, ilasmDir);
            if (processExitCode != 0)
            {
                throw new Exception("ilasm failed, non-zero exit code");
            }
            File.Copy(
                Path.Combine(Path.GetDirectoryName(ilFile), Path.GetFileNameWithoutExtension(ilFile) + ".dll"),
                modAssemblyCSharpDll,
                true);
            Console.WriteLine("ilasm done");
        }

        private static void unpackUabeAssets(
            string autouabeExe, string uabeTempDir, string uabeAssetDumpDir,
            IEnumerable<string> assetFilePaths, string dllDir,
            string stringsDir, string imagesDir, string fontsDir)
        {
            var tempFiles = new List<string>();
            try
            {
                string openListFile;
                FileStream openListFileStream;
                getTempFile(uabeTempDir, out openListFile, out openListFileStream);
                tempFiles.Add(openListFile);
                using (openListFileStream)
                using (var sw = new StreamWriter(openListFileStream, new UTF8Encoding(false)))
                {
                    bool first = true;
                    foreach (string assetFile in assetFilePaths)
                    {
                        if (!first) sw.WriteLine();
                        first = false;
                        sw.Write(assetFile);
                    }
                }

                string notifyFile;
                FileStream notifyFileStream;
                getTempFile(uabeTempDir, out notifyFile, out notifyFileStream);
                tempFiles.Add(notifyFile);
                notifyFileStream.Dispose();

                string autouabeDir = Path.GetDirectoryName(autouabeExe);
                var processArgs = new string[] {
                    "bulkexport", "--openlist", openListFile, "--dlldir", dllDir,
                    "--exportdir", uabeAssetDumpDir, "--notifyfile", notifyFile };
                Console.WriteLine("Running AutoUABE");
                Console.WriteLine("Waiting for AutoUABE");
                Console.WriteLine("(You can check the progress in AutoUABE's window)");
                ProcessUtil.RunBackgroundProcess(autouabeExe, processArgs, autouabeDir);

                string[] notifyFileLines = File.ReadAllLines(notifyFile);
                if (notifyFileLines[0] != "1")
                    throw new Exception("AutoUABE failed, notifyfile content not 1");
                Console.WriteLine("AutoUABE done");

                Console.WriteLine();
                Console.WriteLine("Unpacking UABE assets");
                UabeAssetsEditor.UnpackUabeAssets(uabeAssetDumpDir, stringsDir, imagesDir, fontsDir);
            }
            finally
            {
                tempFiles.ForEach(File.Delete);
            }
        }

        private static void repackUabeAssets(
            string autouabeExe, string uabeTempDir, string uabeAssetDumpDir,
            IEnumerable<string> originalAssetFilePaths, string originalDllDir,
            string uabeStringsDir, string uabeImagesDir, string uabeFontsDir,
            string uabeAssetsSaveDir, string uabeModGameDataDir,
            string uabeModAssetsDir)
        {
            Console.WriteLine("Repacking UABE assets");
            UabeAssetsEditor.RepackUabeAssets(
                uabeAssetDumpDir, uabeModAssetsDir, uabeStringsDir,
                uabeImagesDir, uabeFontsDir);

            if (Directory.EnumerateFiles(uabeModAssetsDir).Any())
            {
                var tempFiles = new List<string>();
                try
                {
                    string openListFile;
                    FileStream openListFileStream;
                    getTempFile(uabeTempDir, out openListFile, out openListFileStream);
                    tempFiles.Add(openListFile);
                    using (openListFileStream)
                    using (var sw = new StreamWriter(openListFileStream, new UTF8Encoding(false)))
                    {
                        bool first = true;
                        foreach (string assetFile in originalAssetFilePaths)
                        {
                            if (!first) sw.WriteLine();
                            first = false;
                            sw.Write(assetFile);
                        }
                    }

                    string notifyFile;
                    FileStream notifyFileStream;
                    getTempFile(uabeTempDir, out notifyFile, out notifyFileStream);
                    tempFiles.Add(notifyFile);
                    notifyFileStream.Dispose();

                    string autouabeDir = Path.GetDirectoryName(autouabeExe);
                    var processArgs = new string[] {
                        "bulkimport", "--openlist", openListFile, "--dlldir", originalDllDir,
                        "--importdir", uabeModAssetsDir, "--savedir", uabeAssetsSaveDir,
                        "--notifyfile", notifyFile };
                    Console.WriteLine();
                    Console.WriteLine("Running AutoUABE");
                    Console.WriteLine("Waiting for AutoUABE");
                    Console.WriteLine("(You can check the progress in AutoUABE's window)");
                    ProcessUtil.RunBackgroundProcess(autouabeExe, processArgs, autouabeDir);

                    string[] notifyFileLines = File.ReadAllLines(notifyFile);
                    if (notifyFileLines[0] != "1")
                        throw new Exception("AutoUABE failed, notifyfile content not 1");
                    Console.WriteLine("AutoUABE done");
                }
                finally
                {
                    tempFiles.ForEach(File.Delete);
                }
            }

            foreach (string saveAssetFile in Directory.EnumerateFiles(uabeAssetsSaveDir))
            {
                File.Copy(
                    saveAssetFile,
                    Path.Combine(uabeModGameDataDir, Path.GetFileName(saveAssetFile)),
                    true);
            }
        }

        /*
         * Catch the exception thrown by FileMode.CreateNew when the file already exists
         * to try to create a temp file.
         */
        private static void getTempFile(
            string dir, out string openListFile, out FileStream openListFileStream)
        {
            var random = new Random();
            var buf = new byte[8];
            int attempts = 0;
            IOException ex = null;
            while (true)
            {
                if (attempts >= 1000)
                    throw new Exception("Failed to create temp file", ex);

                random.NextBytes(buf);
                string hex = BitConverter.ToString(buf).Replace("-", string.Empty);
                string fileName = hex + ".tmp";
                string filePath = Path.Combine(dir, fileName);
                try
                {
                    var fs = File.Open(filePath, FileMode.CreateNew, FileAccess.Write);
                    openListFile = filePath;
                    openListFileStream = fs;
                    break;
                }
                catch (IOException e)
                {
                    ex = e;
                    if (!File.Exists(filePath)) throw e;
                }
                attempts++;
            }
        }

    }
}
