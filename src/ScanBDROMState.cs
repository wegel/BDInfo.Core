using System;
using System.Collections.Generic;

namespace BDInfo
{
    public class ScanBDROMState
    {
        public Exception Exception = null;
        public long FinishedBytes = 0;
        public Dictionary<string, List<TSPlaylistFile>> PlaylistMap = new();
        public TSStreamFile StreamFile = null;
        public DateTime TimeStarted = DateTime.Now;
        public long TotalBytes = 0;
    }
}