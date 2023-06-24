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
    public static class ProcessUtil
    {

        /*
         * Runs a process in the background, without focusing the program.
         * 
         * Throws an exception if the process could not be ran.
         * 
         * Returns the exit code of the process.
         */

        public static int RunBackgroundProcess(
            string processFilePath, IEnumerable<string> processArgs, string processWorkingDirectory)
        {
            if (processFilePath == null)
                throw new ArgumentException("Null file path");

            if (!File.Exists(processFilePath))
                throw new FileNotFoundException(processFilePath);

            string processFilePathFull = Path.GetFullPath(processFilePath);

            string processWorkingDirectoryFull;
            if (processWorkingDirectory == null)
            {
                processWorkingDirectoryFull = null;
            }
            else
            {
                if (!Directory.Exists(processWorkingDirectory))
                    throw new DirectoryNotFoundException(processWorkingDirectory);
                processWorkingDirectoryFull = Path.GetFullPath(processWorkingDirectory);
            }

            string cmd;
            if (processArgs == null)
            {
                cmd = makeWinCmdProcessArgs(new string[] { processFilePathFull });
            }
            else
            {
                cmd = makeWinCmdProcessArgs(new string[] { processFilePathFull }.Concat(processArgs));
            }

            return StartProcessNoActivate(cmd, processWorkingDirectoryFull);
        }

        private static string makeWinCmdProcessArgs(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (string arg in args)
            {
                if (!first) sb.Append(' ');
                first = false;
                sb.Append("\"" + Regex.Replace(arg, @"(\\+)$", @"$1$1") + "\"");
            }
            return sb.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll")]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(
            IntPtr hProcess,
            out IntPtr lpExitCode
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int STARTF_USESHOWWINDOW = 1;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOWMINNOACTIVE = 7;
        private const int DETACHED_PROCESS = 8;

        private static int StartProcessNoActivate(string cmdLine, string workingDir)
        {
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = SW_SHOWMINNOACTIVE;

            var pi = new PROCESS_INFORMATION();

            if (!CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true,
                DETACHED_PROCESS, IntPtr.Zero, workingDir, ref si, out pi))
            {
                throw new Exception("Failed to create process: " + cmdLine);
            }

            try
            {
                IntPtr hProcess = pi.hProcess;
                int processId = pi.dwProcessId;

                Process process;
                try
                {
                    process = Process.GetProcessById(processId);
                }
                catch (ArgumentException)
                {
                    process = null;
                }
                process?.WaitForExit();

                IntPtr lpExitCode = IntPtr.Zero;
                if (!GetExitCodeProcess(hProcess, out lpExitCode))
                    throw new Exception("Failed to get process exit code");
                int exitCode = (byte)lpExitCode.ToInt64();

                return exitCode;
            }
            finally
            {
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
        }

    }
}
