using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Synacor.Utils {
    public sealed class ConsoleHelper : IDisposable {
        SafeFileHandle safeFileHandle;
        FileStream fileStream;
        public StreamWriter StandardOutput { get; }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint OPEN_EXISTING = 0x3;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            uint lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            uint hTemplateFile
        );

        public ConsoleHelper() {
            //read this https://developercommunity.visualstudio.com/content/problem/12166/console-output-is-gone-in-vs2017-works-fine-when-d.html
            AllocConsole();
            IntPtr stdHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);
            safeFileHandle = new SafeFileHandle(stdHandle, true);
            fileStream = new FileStream(safeFileHandle, FileAccess.Write);
            StandardOutput = new StreamWriter(fileStream, Console.OutputEncoding);
            StandardOutput.AutoFlush = true;
            Console.SetOut(StandardOutput);
            Console.WriteLine("rerer");
        }

        public void Dispose() {
            safeFileHandle?.Dispose();
            fileStream?.Dispose();
            StandardOutput?.Dispose();
            FreeConsole();
        }
    }
}