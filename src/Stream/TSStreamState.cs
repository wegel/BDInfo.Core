namespace BDInfo
{
    public record TSStreamState
    {
        public ulong TransferCount;

        public string StreamTag;

        public ulong TotalPackets;
        public ulong WindowPackets;

        public ulong TotalBytes;
        public ulong WindowBytes;

        public long PeakTransferLength;
        public long PeakTransferRate;

        public double TransferMarker = 0;
        public double TransferInterval = 0;

        public readonly TSStreamBuffer StreamBuffer = new();

        public uint Parse;
        public bool TransferState;
        public int TransferLength;
        public int PacketLength;
        public byte PacketLengthParse;
        public byte PacketParse;

        public byte PTSParse;
        public ulong PTS;
        public ulong PTSTemp;
        public ulong PTSLast;
        public ulong PTSPrev = 0;
        public ulong PTSDiff;
        public ulong PTSCount;
        public ulong PTSTransfer;

        public byte DTSParse;
        public ulong DTSTemp;
        public ulong DTSPrev;

        public byte PESHeaderLength;
        public byte PESHeaderFlags;
#if DEBUG
        public byte PESHeaderIndex = 0;
        public byte[] PESHeader = new byte[256 + 9];
#endif
    }
}