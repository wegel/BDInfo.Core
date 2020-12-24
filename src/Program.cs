using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace BDInfo
{
    internal static class Program
    {
        private static void Main(string bdPath, bool list=false, bool whole=false, List<string> playlists=null)
        {
            if (list) whole = true;

            if (!File.Exists(bdPath) && !Directory.Exists(bdPath))
            {
                Console.Error.WriteLine($"error: {bdPath} does not exist");
                Environment.Exit(-1);
            }

            var main = new BDROMScanner(new DoWorkEventArgs(bdPath));
            Console.WriteLine("Please wait while we scan the disc...");
            if (playlists != null)
            {
                main.LoadPlaylists(playlists);
            }
            else if (whole)
            {
                main.LoadPlaylists(true);
            }
            else
            {
                main.LoadPlaylists();
            }

            if (list) Environment.Exit(0);
            main.ScanBDROMWork(null, null);
            main.GenerateReportCLI();
        }
    }
}