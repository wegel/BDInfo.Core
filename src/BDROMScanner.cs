using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace BDInfo
{
    public class BDROMScanner
    {
        private readonly BDROM _bdrom;
        private ScanBDROMResult _scanResult = new();
        private List<TSPlaylistFile> _selectedPlaylists = new();

        public BDROMScanner(DoWorkEventArgs eventArgs)
        {
            _bdrom = new BDROM((string) eventArgs.Argument);
            _bdrom.StreamClipFileScanError += BDROM_StreamClipFileScanError;
            _bdrom.StreamFileScanError += BDROM_StreamFileScanError;
            _bdrom.PlaylistFileScanError += BDROM_PlaylistFileScanError;
            _bdrom.Scan();
        }

        private static bool BDROM_PlaylistFileScanError(TSPlaylistFile playlistFile, Exception ex)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "An error occurred while scanning the playlist file {0}.\n\nThe disc may be copy-protected or damaged.\n\nDo you want to continue scanning the playlist files?",
                playlistFile.Name));

            return true;
        }

        private static bool BDROM_StreamFileScanError(TSStreamFile streamFile, Exception ex)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "An error occurred while scanning the stream file {0}.\n\nThe disc may be copy-protected or damaged.\n\nDo you want to continue scanning the stream files?",
                streamFile.Name));

            return true;
        }

        private static bool BDROM_StreamClipFileScanError(TSStreamClipFile streamClipFile, Exception ex)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "An error occurred while scanning the stream clip file {0}.\n\nThe disc may be copy-protected or damaged.\n\nDo you want to continue scanning the stream clip files?",
                streamClipFile.Name));

            return true;
        }

        internal void ScanBDROMWork(object sender, DoWorkEventArgs e)
        {
            _scanResult = new ScanBDROMResult {ScanException = new Exception("Scan is still running.")};

            var streamFiles = new List<TSStreamFile>();

            Console.WriteLine("Preparing to analyze the following:");
            // Adapted from ScanBDROM()
            foreach (var playlist in _selectedPlaylists)
            {
                Console.Write("{0} --> ", playlist.Name);
                var streamNames = new List<string>();
                foreach (var clip in playlist.StreamClips.Where(clip => !streamFiles.Contains(clip.StreamFile)))
                {
                    streamNames.Add(clip.StreamFile.Name);
                    streamFiles.Add(clip.StreamFile);
                }

                Console.WriteLine(string.Join(" + ", streamNames));
            }

            Timer timer = null;
            try
            {
                var scanState = new ScanBDROMState();
                foreach (var streamFile in streamFiles)
                {
                    if (BDInfoSettings.EnableSSIF && streamFile.InterleavedFile != null)
                    {
                        if (streamFile.InterleavedFile.FileInfo != null)
                            scanState.TotalBytes += streamFile.InterleavedFile.FileInfo.Length;
                        else
                            scanState.TotalBytes += streamFile.InterleavedFile.DFileInfo.Length;
                    }
                    else
                    {
                        if (streamFile.FileInfo != null)
                            scanState.TotalBytes += streamFile.FileInfo.Length;
                        else
                            scanState.TotalBytes += streamFile.DFileInfo.Length;
                    }

                    if (!scanState.PlaylistMap.ContainsKey(streamFile.Name)) scanState.PlaylistMap[streamFile.Name] = new List<TSPlaylistFile>();

                    foreach (var playlist in _bdrom.PlaylistFiles.Values)
                    {
                        playlist.ClearBitrates();

                        foreach (var clip in playlist.StreamClips)
                            if (clip.Name == streamFile.Name)
                                if (!scanState.PlaylistMap[streamFile.Name].Contains(playlist))
                                    scanState.PlaylistMap[streamFile.Name].Add(playlist);
                    }
                }

                timer = new Timer(ScanBDROMProgress, scanState, 1000, 1000);
                Console.WriteLine("\n{0,16}{1,-15}{2,-13}{3}", "", "File", "Elapsed", "Remaining");

                foreach (var streamFile in streamFiles)
                {
                    scanState.StreamFile = streamFile;

                    var thread = new Thread(ScanBDROMThread);
                    thread.Start(scanState);
                    while (thread.IsAlive) Thread.Sleep(250);
                    if (streamFile.FileInfo != null)
                        scanState.FinishedBytes += streamFile.FileInfo.Length;
                    else
                        scanState.FinishedBytes += streamFile.DFileInfo.Length;
                    if (scanState.Exception != null) _scanResult.FileExceptions[streamFile.Name] = scanState.Exception;
                }

                _scanResult.ScanException = null;
            }
            catch (Exception ex)
            {
                _scanResult.ScanException = ex;
            }
            finally
            {
                Console.WriteLine();
                timer?.Dispose();
            }
        }

        private void ScanBDROMThread(object parameter)
        {
            var scanState = (ScanBDROMState) parameter;
            try
            {
                var streamFile = scanState.StreamFile;
                var playlists = scanState.PlaylistMap[streamFile.Name];
                streamFile.Scan(playlists, true);
            }
            catch (Exception ex)
            {
                scanState.Exception = ex;
            }
        }

        private void ScanBDROMProgress(object state)
        {
            var scanState = (ScanBDROMState) state;

            var finishedBytes = scanState.FinishedBytes;
            if (scanState.StreamFile != null) finishedBytes += scanState.StreamFile.Size;

            var progress = (double) finishedBytes / scanState.TotalBytes;
            var progressValue = (int) Math.Round(progress * 100);
            if (progressValue < 0) progressValue = 0;
            if (progressValue > 100) progressValue = 100;

            var elapsedTime = DateTime.Now.Subtract(scanState.TimeStarted);
            TimeSpan remainingTime;
            if (progress > 0 && progress < 1)
                remainingTime = new TimeSpan((long) (elapsedTime.Ticks / progress) - elapsedTime.Ticks);
            else
                remainingTime = new TimeSpan(0);

            var elapsedTimeString = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", elapsedTime.Hours, elapsedTime.Minutes,
                elapsedTime.Seconds);

            var remainingTimeString = string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", remainingTime.Hours, remainingTime.Minutes,
                remainingTime.Seconds);

            if (scanState.StreamFile != null)
                Console.Write("Scanning {0,3:d}% - {1,10} {2,12}  |  {3}\r", progressValue, scanState.StreamFile.DisplayName, elapsedTimeString,
                    remainingTimeString);
            else
                Console.Write("Scanning {0,3}% - \t{1,10}  |  {2}...\r", progressValue, elapsedTimeString, remainingTimeString);
        }

        public void GenerateReportCLI(string savePath)
        {
            if (_scanResult.ScanException != null)
            {
                Console.WriteLine("{0}", _scanResult.ScanException.Message);
            }
            else
            {
                Console.WriteLine(_scanResult.FileExceptions.Count > 0 ? "Scan completed with errors (see report)." : "Scan completed successfully.");
                Console.WriteLine("Please wait while we generate the report...");
                Console.Write(Report.Generate(_bdrom, _selectedPlaylists, _scanResult));
            }
        }

        public void LoadPlaylists(List<string> inputPlaylists)
        {
            _selectedPlaylists = new List<TSPlaylistFile>();
            foreach (var playlistName in inputPlaylists)
            {
                var name = playlistName.ToUpper();
                if (!_bdrom.PlaylistFiles.ContainsKey(name)) continue;
                if (!_selectedPlaylists.Contains(_bdrom.PlaylistFiles[name])) _selectedPlaylists.Add(_bdrom.PlaylistFiles[name]);
            }

            // throw error if no playlist is found
            if (_selectedPlaylists.Count == 0) throw new Exception("No matching playlists found on BD");
        }

        public void LoadPlaylists(bool wholeDisc = false)
        {
            _selectedPlaylists = new List<TSPlaylistFile>();

            if (_bdrom == null) return;

            var hasHiddenTracks = false;
            var groups = new List<List<TSPlaylistFile>>();

            var sortedPlaylistFiles = new TSPlaylistFile[_bdrom.PlaylistFiles.Count];
            _bdrom.PlaylistFiles.Values.CopyTo(sortedPlaylistFiles, 0);
            Array.Sort(sortedPlaylistFiles, ComparePlaylistFiles);

            foreach (var playlist1 in sortedPlaylistFiles)
            {
                if (!playlist1.IsValid) continue;

                var matchingGroupIndex = 0;
                for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    foreach (var playlist2 in groups[groupIndex].Where(playlist2 => playlist2.IsValid))
                    {
                        foreach (var clip1 in playlist1.StreamClips)
                        {
                            if (playlist2.StreamClips.Any(clip2 => clip1.Name == clip2.Name)) matchingGroupIndex = groupIndex + 1;

                            if (matchingGroupIndex > 0) break;
                        }

                        if (matchingGroupIndex > 0) break;
                    }

                    if (matchingGroupIndex > 0) break;
                }

                if (matchingGroupIndex > 0)
                    groups[matchingGroupIndex - 1].Add(playlist1);
                else
                    groups.Add(new List<TSPlaylistFile> {playlist1});
            }

            Console.WriteLine("{0,-4}{1,-7}{2,-15}{3,-10}{4,-16}{5,-16}\n", "#", "Group", "Playlist File", "Length", "Estimated Bytes", "Measured Bytes");
            var playlistIdx = 1;
            var playlistDict = new Dictionary<int, TSPlaylistFile>();

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                group.Sort(ComparePlaylistFiles);

                foreach (var playlist in group.Where(playlist => playlist.IsValid))
                {
                    playlistDict[playlistIdx] = playlist;
                    if (wholeDisc) _selectedPlaylists.Add(playlist);

                    if (playlist.HasHiddenTracks) hasHiddenTracks = true;

                    var groupString = (groupIndex + 1).ToString();

                    var playlistLengthSpan = new TimeSpan((long) (playlist.TotalLength * 10000000));
                    var length = $"{playlistLengthSpan.Hours:D2}:{playlistLengthSpan.Minutes:D2}:{playlistLengthSpan.Seconds:D2}";

                    string fileSize;
                    if (BDInfoSettings.EnableSSIF && playlist.InterleavedFileSize > 0)
                        fileSize = playlist.InterleavedFileSize.ToString("N0");
                    else if (playlist.FileSize > 0)
                        fileSize = playlist.FileSize.ToString("N0");
                    else
                        fileSize = "-";

                    var fileSize2 = playlist.TotalAngleSize > 0 ? playlist.TotalAngleSize.ToString("N0") : "-";

                    Console.WriteLine("{0,-4:G}{1,-7}{2,-15}{3,-10}{4,-16}{5,-16}", playlistIdx.ToString(), groupString, playlist.Name, length, fileSize,
                        fileSize2);
                    playlistIdx++;
                }
            }

            if (hasHiddenTracks) Console.WriteLine("(*) Some playlists on this disc have hidden tracks. These tracks are marked with an asterisk.");
            if (wholeDisc) return;

            for (int selectedIdx; (selectedIdx = GetIntIndex(1, playlistIdx - 1)) > 0;)
            {
                _selectedPlaylists.Add(playlistDict[selectedIdx]);
                Console.WriteLine("Added {0}", selectedIdx);
            }

            if (_selectedPlaylists.Count != 0) return;
            Console.WriteLine("No playlists selected. Exiting.");
            Environment.Exit(0);
        }

        internal void LoadPlaylists()
        {
            throw new NotImplementedException();
        }

        /* XXX: returns -1 on 'q' input */
        private static int GetIntIndex(int min, int max)
        {
            var resp = -1;
            do
            {
                while (Console.KeyAvailable) Console.ReadKey();

                Console.Write("Select (q when finished): ");
                var response = Console.ReadLine();
                if (response == "q") return -1;

                try
                {
                    resp = int.Parse(response);
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid Input!");
                }

                if (resp > max || resp < min) Console.WriteLine("Invalid Selection!");
            } while (resp > max || resp < min);

            Console.WriteLine();

            return resp;
        }

        private static int ComparePlaylistFiles(TSPlaylistFile x, TSPlaylistFile y)
        {
            switch (x)
            {
                case null when y == null:
                    return 0;
                case null when y != null:
                    return 1;
            }

            if (x != null && y == null) return -1;

            if (x.TotalLength > y.TotalLength) return -1;
            if (y.TotalLength > x.TotalLength) return 1;

            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }
}