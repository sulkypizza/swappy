using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace SteamGameLauncherHelper
{
    internal class Program
    {
        static int maxTry = 600;
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                MessageBox.Show("Usage: \"[Launch exe]\" \"[Launch args]\" \"[Watch exe]\"");
                return;
            }

            Process launchProces = new Process();
            launchProces.StartInfo.FileName = args[0];
            launchProces.StartInfo.Arguments = args[1];

            launchProces.Start();

            Process foundProcess = null;
            while (foundProcess == null)
            {
                if (maxTry <= 0)
                    return;

                Thread.Sleep(1000);
                maxTry -= 1;
                foundProcess = GetProcess(args[2]);
            }

            foundProcess.WaitForExit();
        }

        private static Process GetProcess(string path)
        {
            Process[] plist = Process.GetProcesses();

            for (int i = 0; i < plist.Length; i++)
            {
                StringBuilder builder = new StringBuilder(Int16.MaxValue);

                IntPtr ptr = OpenProcess(0x00001000, false, plist[i].Id);
                int wordSize = Int16.MaxValue;

                if (QueryFullProcessImageName(ptr, 0, builder, ref wordSize))
                    if (builder.ToString().ToLower() == path.ToLower())
                        return plist[i];
            }

            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
        int processAccess,
        bool bInheritHandle,
        int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(
        [In] IntPtr hProcess,
        [In] int dwFlags,
        [Out] StringBuilder lpExeName,
        ref int lpdwSize);
    }
}

