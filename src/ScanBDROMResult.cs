using System;
using System.Collections.Generic;

namespace BDInfo
{
    public class ScanBDROMResult
    {
        public Dictionary<string, Exception> FileExceptions = new();
        public Exception ScanException = new("Scan has not been run.");
    }
}