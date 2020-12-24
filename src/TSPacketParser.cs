using System.Collections.Generic;

namespace BDInfo
{
    public record TSPacketParser
    {
        public byte AdaptionFieldControl;
        public byte AdaptionFieldLength;
        public byte AdaptionFieldParse;

        public bool AdaptionFieldState;
        public byte HeaderParse;
        public byte PacketLength;

        public readonly byte[] PAT = new byte[1024];
        public byte PATLastSectionNumber;
        public uint PATOffset;
        public byte PATPointerField;
        public ushort PATSectionLength;
        public byte PATSectionLengthParse;
        public byte PATSectionNumber;
        public uint PATSectionParse;
        public bool PATSectionStart;
        public bool PATTransferState;
        public byte PayloadUnitStartIndicator;
        public ulong PCR;
        public ulong PCRCount;
        public byte PCRParse;

        public ushort PCRPID = 0xFFFF;
        public ushort PID;
        public readonly Dictionary<ushort, byte[]> PMT = new();
        public byte PMTLastSectionNumber;
        public uint PMTOffset;
        public ushort PMTPID = 0xFFFF;
        public byte PMTPointerField;
        public byte PMTProgramDescriptor;
        public byte PMTProgramDescriptorLength;
        public byte PMTProgramDescriptorLengthParse;

        public readonly List<TSDescriptor> PMTProgramDescriptors = new();
        public ushort PMTProgramInfoLength;
        public ushort PMTSectionLength;
        public uint PMTSectionLengthParse;
        public byte PMTSectionNumber;
        public uint PMTSectionParse;
        public bool PMTSectionStart;
        public uint PMTStreamDescriptorLength = 0;
        public uint PMTStreamDescriptorLengthParse = 0;
        public ushort PMTStreamInfoLength = 0;

        public byte PMTTemp;
        public bool PMTTransferState;
        public ulong PreviousPCR;
        public ulong PTSDiff = 0;
        public ulong PTSFirst = ulong.MaxValue;
        public ulong PTSLast = ulong.MinValue;

        public TSStream Stream;
        public TSStreamState StreamState;
        public bool SyncState;

        public uint TimeCode;
        public byte TimeCodeParse = 4;

        public ulong TotalPackets;
        public byte TransportErrorIndicator;
        public byte TransportPriority;
        public byte TransportScramblingControl;

        public ushort TransportStreamId = 0xFFFF;
    }
}