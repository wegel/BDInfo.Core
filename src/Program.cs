using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Options;

namespace BDInfo
{
    internal static class Program
    {
        private static void show_help(OptionSet optionSet, string msg = null)
        {
            if (msg != null) Console.Error.WriteLine(msg);
            Console.Error.WriteLine("Usage: BDInfo.exe <BD_PATH> [REPORT_DEST]");
            Console.Error.WriteLine("BD_PATH may be a directory containing a BDMV folder or a BluRay ISO file.");
            Console.Error.WriteLine("REPORT_DEST is the folder the BDInfo report is to be written to. If not");
            Console.Error.WriteLine("given, the report will be written to BD_PATH. REPORT_DEST is required if");
            Console.Error.WriteLine("BD_PATH is an ISO file.\n");
            optionSet.WriteOptionDescriptions(Console.Error);
            Environment.Exit(-1);
        }

        private static void Main(string[] args)
        {
            var help = false;
            var version = false;
            var whole = false;
            var list = false;
            string mpls = null;
            var optionSet = new OptionSet().Add("h|help", "Print out the options.", option => help = option != null)
                .Add("l|list", "Print the list of playlists.", option => list = option != null)
                .Add("m=|mpls=", "Comma separated list of playlists to scan.", option => mpls = option)
                .Add("w|whole", "Scan whole disc - every playlist.", option => whole = option != null)
                .Add("v|version", "Print the version.", option => version = option != null);

            var nsargs = new List<string>();
            try
            {
                nsargs = optionSet.Parse(args);
            }
            catch (OptionException)
            {
                show_help(optionSet, "Error - usage is:");
            }

            if (help) show_help(optionSet);

            if (version)
            {
                Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                Environment.Exit(-1);
            }

            if (list) whole = true;

            var bdPath = nsargs[0];
            if (!File.Exists(bdPath) && !Directory.Exists(bdPath))
            {
                Console.Error.WriteLine($"error: {bdPath} does not exist");
                Environment.Exit(-1);
            }

            var reportPath = bdPath;
            switch (nsargs.Count)
            {
                case 0:
                    show_help(optionSet, "Error: insufficient args - usage is:");
                    Environment.Exit(-1);
                    break;
                case 1 when !Directory.Exists(bdPath):
                    Console.Error.WriteLine("error: REPORT_DEST must be given if BD_PATH is an ISO.");
                    Environment.Exit(-1);
                    break;
                case 2:
                    reportPath = nsargs[1];
                    break;
            }

            if (!Directory.Exists(reportPath))
            {
                Console.Error.WriteLine($"error: {reportPath} does not exist or is not a directory");
                Environment.Exit(-1);
            }

            var main = new BDROMScanner(new DoWorkEventArgs(bdPath));
            Console.WriteLine("Please wait while we scan the disc...");
            if (mpls != null)
            {
                Console.WriteLine(mpls);
                main.LoadPlaylists(mpls.Split(',').ToList());
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
            main.GenerateReportCLI(reportPath);
        }
    }
}