﻿#undef DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using DiscUtils;
using DiscUtils.Udf;

namespace BDInfo
{
    public class TSStreamFile
    {
        private readonly Dictionary<ushort, TSStreamState> _streamStates = new();
        private readonly UdfReader CdReader;
        public readonly DiscFileInfo DFileInfo;

        public readonly FileInfo FileInfo;

        public TSInterleavedFile InterleavedFile = null;
        public double Length;
        public readonly string Name;

        private List<TSPlaylistFile> _playlists;
        public long Size;

        public Dictionary<ushort, List<TSStreamDiagnostics>> StreamDiagnostics = new();

        public Dictionary<ushort, TSStream> Streams = new();

        public TSStreamFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            DFileInfo = null;
            CdReader = null;
            Name = fileInfo.Name.ToUpper();
        }

        public TSStreamFile(DiscFileInfo fileInfo, UdfReader reader)
        {
            DFileInfo = fileInfo;
            FileInfo = null;
            CdReader = reader;
            Name = fileInfo.Name.ToUpper();
        }

        public string DisplayName
        {
            get
            {
                if (BDInfoSettings.EnableSSIF && InterleavedFile != null) return InterleavedFile.Name;
                return Name;
            }
        }

        public string GetFilePath()
        {
            if (!string.IsNullOrEmpty(FileInfo?.FullName)) return FileInfo.FullName;

            if (!string.IsNullOrEmpty(DFileInfo?.FullName)) return DFileInfo.FullName;

            return string.Empty;
        }

        private bool ScanStream(TSStream stream, TSStreamState streamState, TSStreamBuffer buffer)
        {
            streamState.StreamTag = null;
            long bitrate = 0;
            if (stream.IsAudioStream && streamState.PTSTransfer > 0)
            {
                bitrate = (long) Math.Round(buffer.TransferLength * 8.0 / ((double) streamState.PTSTransfer / 90000));
                if (bitrate > streamState.PeakTransferRate) streamState.PeakTransferRate = bitrate;
            }

            if (buffer.TransferLength > streamState.PeakTransferLength) streamState.PeakTransferLength = buffer.TransferLength;
            buffer.BeginRead();
            switch (stream.StreamType)
            {
                case TSStreamType.MPEG2_VIDEO:
                    (new TSCodecMPEG2()).Scan((TSVideoStream) stream, buffer, ref streamState.StreamTag);
                    break;

                case TSStreamType.AVC_VIDEO:
                    (new TSCodecAVC()).Scan((TSVideoStream) stream, buffer, ref streamState.StreamTag);
                    break;

                case TSStreamType.MVC_VIDEO:
                    (new TSCodecMVC()).Scan((TSVideoStream) stream, buffer, ref streamState.StreamTag);
                    break;

                case TSStreamType.HEVC_VIDEO:
                    (new TSCodecHEVC()).Scan((TSVideoStream) stream, buffer, ref streamState.StreamTag);
                    break;

                case TSStreamType.VC1_VIDEO:
                    (new TSCodecVC1()).Scan((TSVideoStream) stream, buffer, ref streamState.StreamTag);
                    break;

                case TSStreamType.AC3_AUDIO:
                    (new TSCodecAC3()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                case TSStreamType.AC3_PLUS_AUDIO:
                case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                    (new TSCodecAC3()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                case TSStreamType.AC3_TRUE_HD_AUDIO:
                    (new TSCodecTrueHD()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                case TSStreamType.LPCM_AUDIO:
                    (new TSCodecLPCM()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                case TSStreamType.DTS_AUDIO:
                    (new TSCodecDTS()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                case TSStreamType.DTS_HD_AUDIO:
                case TSStreamType.DTS_HD_MASTER_AUDIO:
                case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                    (new TSCodecDTSHD()).Scan((TSAudioStream) stream, buffer, ref streamState.StreamTag, bitrate);
                    break;

                default:
                    stream.IsInitialized = true;
                    break;
            }

            buffer.EndRead();
            streamState.StreamBuffer.Reset();

            var isAVC = false;
            var isMVC = false;
            foreach (var finishedStream in Streams.Values)
            {
                if (!finishedStream.IsInitialized) return false;
                if (finishedStream.StreamType == TSStreamType.AVC_VIDEO) isAVC = true;
                if (finishedStream.StreamType == TSStreamType.MVC_VIDEO) isMVC = true;
            }

            if (isMVC && !isAVC) return false;
            return true;
        }

        private void UpdateStreamBitrates(ushort PTSPID, ulong PTS, ulong PTSDiff)
        {
            if (_playlists == null) return;

            foreach (var PID in _streamStates.Keys)
            {
                if (Streams.ContainsKey(PID) && Streams[PID].IsVideoStream && PID != PTSPID) continue;
                if (_streamStates[PID].WindowPackets == 0) continue;
                UpdateStreamBitrate(PID, PTSPID, PTS, PTSDiff);
            }

            foreach (var playlist in _playlists)
            {
                double packetSeconds = 0;
                foreach (var clip in playlist.StreamClips)
                    if (clip.AngleIndex == 0)
                        packetSeconds += clip.PacketSeconds;
                if (packetSeconds > 0)
                    foreach (var playlistStream in playlist.SortedStreams)
                        if (playlistStream.IsVBR)
                            playlistStream.BitRate = (long) Math.Round(playlistStream.PayloadBytes * 8.0 / packetSeconds);
            }
        }

        private void UpdateStreamBitrate(ushort PID, ushort PTSPID, ulong PTS, ulong PTSDiff)
        {
            if (_playlists == null) return;

            var streamState = _streamStates[PID];
            var streamTime = (double) PTS / 90000;
            var streamInterval = (double) PTSDiff / 90000;
            var streamOffset = streamTime + streamInterval;

            foreach (var playlist in _playlists)
            foreach (var clip in playlist.StreamClips)
            {
                if (clip.Name != Name) continue;

                if (streamTime == 0 || streamTime >= clip.TimeIn && streamTime <= clip.TimeOut)
                {
                    clip.PayloadBytes += streamState.WindowBytes;
                    clip.PacketCount += streamState.WindowPackets;

                    if (streamOffset > clip.TimeIn && streamOffset - clip.TimeIn > clip.PacketSeconds) clip.PacketSeconds = streamOffset - clip.TimeIn;

                    var playlistStreams = playlist.Streams;
                    if (clip.AngleIndex > 0 && clip.AngleIndex < playlist.AngleStreams.Count + 1) playlistStreams = playlist.AngleStreams[clip.AngleIndex - 1];
                    if (playlistStreams.ContainsKey(PID))
                    {
                        var stream = playlistStreams[PID];

                        stream.PayloadBytes += streamState.WindowBytes;
                        stream.PacketCount += streamState.WindowPackets;

                        if (stream.IsVideoStream)
                        {
                            stream.PacketSeconds += streamInterval;

                            stream.ActiveBitRate = (long) Math.Round(stream.PayloadBytes * 8.0 / stream.PacketSeconds);
                        }

                        if (stream.StreamType == TSStreamType.AC3_TRUE_HD_AUDIO && ((TSAudioStream) stream).CoreStream != null)
                            stream.ActiveBitRate -= ((TSAudioStream) stream).CoreStream.BitRate;
                    }
                }
            }

            if (Streams.ContainsKey(PID))
            {
                var stream = Streams[PID];
                stream.PayloadBytes += streamState.WindowBytes;
                stream.PacketCount += streamState.WindowPackets;

                if (stream.IsVideoStream)
                {
                    var diag = new TSStreamDiagnostics
                    {
                        Marker = (double) PTS / 90000,
                        Interval = (double) PTSDiff / 90000,
                        Bytes = streamState.WindowBytes,
                        Packets = streamState.WindowPackets,
                        Tag = streamState.StreamTag
                    };
                    StreamDiagnostics[PID].Add(diag);

                    stream.PacketSeconds += streamInterval;
                }
            }

            streamState.WindowPackets = 0;
            streamState.WindowBytes = 0;
        }

        public void Scan(List<TSPlaylistFile> playlists, bool isFullScan)
        {
            if (playlists == null || playlists.Count == 0) return;

            _playlists = playlists;
            var dataSize = 16384;
            Stream fileStream = null;
            try
            {
                string fileName;
                if (BDInfoSettings.EnableSSIF && InterleavedFile != null)
                {
                    if (InterleavedFile.FileInfo != null)
                        fileName = InterleavedFile.FileInfo.FullName;
                    else
                        fileName = InterleavedFile.DFileInfo.FullName;
                }
                else
                {
                    if (FileInfo != null)
                        fileName = FileInfo.FullName;
                    else
                        fileName = DFileInfo.FullName;
                }

                if (CdReader == null)
                    fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, dataSize, false);
                else
                    fileStream = CdReader.OpenFile(fileName, FileMode.Open, FileAccess.Read);

                Size = 0;
                Length = 0;

                Streams.Clear();
                _streamStates.Clear();
                StreamDiagnostics.Clear();

                var parser = new TSPacketParser();

                long fileLength = (uint) fileStream.Length;
                var buffer = new byte[dataSize];
                var bufferLength = 0;
                while ((bufferLength = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var offset = 0;
                    for (var i = 0; i < bufferLength; i++)
                        if (parser.SyncState == false)
                        {
                            if (parser.TimeCodeParse > 0)
                            {
                                parser.TimeCodeParse--;
                                switch (parser.TimeCodeParse)
                                {
                                    case 3:
                                        parser.TimeCode = 0;
                                        parser.TimeCode |= ((uint) buffer[i] & 0x3F) << 24;
                                        break;
                                    case 2:
                                        parser.TimeCode |= ((uint) buffer[i] & 0xFF) << 16;
                                        break;
                                    case 1:
                                        parser.TimeCode |= ((uint) buffer[i] & 0xFF) << 8;
                                        break;
                                    case 0:
                                        parser.TimeCode |= (uint) buffer[i] & 0xFF;
                                        break;
                                }
                            }
                            else if (buffer[i] == 0x47)
                            {
                                parser.SyncState = true;
                                parser.PacketLength = 187;
                                parser.TimeCodeParse = 4;
                                parser.HeaderParse = 3;
                            }
                        }
                        else if (parser.HeaderParse > 0)
                        {
                            parser.PacketLength--;
                            parser.HeaderParse--;

                            switch (parser.HeaderParse)
                            {
                                case 2:
                                {
                                    parser.TransportErrorIndicator = (byte) ((buffer[i] >> 7) & 0x1);
                                    parser.PayloadUnitStartIndicator = (byte) ((buffer[i] >> 6) & 0x1);
                                    parser.TransportPriority = (byte) ((buffer[i] >> 5) & 0x1);
                                    parser.PID = (ushort) ((buffer[i] & 0x1f) << 8);
                                }
                                    break;

                                case 1:
                                {
                                    parser.PID |= buffer[i];
                                    if (Streams.ContainsKey(parser.PID))
                                        parser.Stream = Streams[parser.PID];
                                    else
                                        parser.Stream = null;
                                    if (!_streamStates.ContainsKey(parser.PID)) _streamStates[parser.PID] = new TSStreamState();
                                    parser.StreamState = _streamStates[parser.PID];
                                    parser.StreamState.TotalPackets++;
                                    parser.StreamState.WindowPackets++;
                                    parser.TotalPackets++;
                                }
                                    break;

                                case 0:
                                {
                                    parser.TransportScramblingControl = (byte) ((buffer[i] >> 6) & 0x3);
                                    parser.AdaptionFieldControl = (byte) ((buffer[i] >> 4) & 0x3);

                                    if ((parser.AdaptionFieldControl & 0x2) == 0x2) parser.AdaptionFieldState = true;
                                    if (parser.PayloadUnitStartIndicator == 1)
                                    {
                                        if (parser.PID == 0)
                                        {
                                            parser.PATSectionStart = true;
                                        }
                                        else if (parser.PID == parser.PMTPID)
                                        {
                                            parser.PMTSectionStart = true;
                                        }
                                        else if (parser.StreamState != null && parser.StreamState.TransferState)
                                        {
                                            parser.StreamState.TransferState = false;
                                            parser.StreamState.TransferCount++;

                                            var isFinished = ScanStream(parser.Stream, parser.StreamState, parser.StreamState.StreamBuffer);

                                            if (!isFullScan && isFinished) return;
                                        }
                                    }
                                }
                                    break;
                            }
                        }
                        else if (parser.AdaptionFieldState)
                        {
                            parser.PacketLength--;
                            parser.AdaptionFieldParse = buffer[i];
                            parser.AdaptionFieldLength = buffer[i];
                            parser.AdaptionFieldState = false;
                        }
                        else if (parser.AdaptionFieldParse > 0)
                        {
                            parser.PacketLength--;
                            parser.AdaptionFieldParse--;
                            if (parser.AdaptionFieldLength - parser.AdaptionFieldParse == 1)
                            {
                                if ((buffer[i] & 0x10) == 0x10)
                                {
                                    parser.PCRParse = 6;
                                    parser.PCR = 0;
                                }
                            }
                            else if (parser.PCRParse > 0)
                            {
                                parser.PCRParse--;
                                parser.PCR = (parser.PCR << 8) + buffer[i];
                                if (parser.PCRParse == 0)
                                {
                                    parser.PreviousPCR = parser.PCR;
                                    parser.PCR = (parser.PCR & 0x1FF) + (parser.PCR >> 15) * 300;
                                }

                                parser.PCRCount++;
                            }

                            if (parser.PacketLength == 0) parser.SyncState = false;
                        }
                        else if (parser.PID == 0)
                        {
                            if (parser.PATTransferState)
                            {
                                if (bufferLength - i > parser.PATSectionLength)
                                    offset = parser.PATSectionLength;
                                else
                                    offset = bufferLength - i;
                                if (parser.PacketLength <= offset) offset = parser.PacketLength;

                                for (var k = 0; k < offset; k++)
                                {
                                    parser.PAT[parser.PATOffset++] = buffer[i++];
                                    parser.PATSectionLength--;
                                    parser.PacketLength--;
                                }

                                --i;

                                if (parser.PATSectionLength == 0)
                                {
                                    parser.PATTransferState = false;
                                    if (parser.PATSectionNumber == parser.PATLastSectionNumber)
                                        for (var k = 0; k < parser.PATOffset - 4; k += 4)
                                        {
                                            var programNumber = (uint) ((parser.PAT[k] << 8) + parser.PAT[k + 1]);

                                            var programPID = (ushort) (((parser.PAT[k + 2] & 0x1F) << 8) + parser.PAT[k + 3]);

                                            if (programNumber == 1) parser.PMTPID = programPID;
                                        }
                                }
                            }
                            else
                            {
                                --parser.PacketLength;
                                if (parser.PATSectionStart)
                                {
                                    parser.PATPointerField = buffer[i];
                                    if (parser.PATPointerField == 0) parser.PATSectionLengthParse = 3;
                                    parser.PATSectionStart = false;
                                }
                                else if (parser.PATPointerField > 0)
                                {
                                    --parser.PATPointerField;
                                    if (parser.PATPointerField == 0) parser.PATSectionLengthParse = 3;
                                }
                                else if (parser.PATSectionLengthParse > 0)
                                {
                                    --parser.PATSectionLengthParse;
                                    switch (parser.PATSectionLengthParse)
                                    {
                                        case 2:
                                            break;
                                        case 1:
                                            parser.PATSectionLength = (ushort) ((buffer[i] & 0xF) << 8);
                                            break;
                                        case 0:
                                            parser.PATSectionLength |= buffer[i];
                                            if (parser.PATSectionLength > 1021)
                                                parser.PATSectionLength = 0;
                                            else
                                                parser.PATSectionParse = 5;
                                            break;
                                    }
                                }
                                else if (parser.PATSectionParse > 0)
                                {
                                    --parser.PATSectionLength;
                                    --parser.PATSectionParse;

                                    switch (parser.PATSectionParse)
                                    {
                                        case 4:
                                            parser.TransportStreamId = (ushort) (buffer[i] << 8);
                                            break;
                                        case 3:
                                            parser.TransportStreamId |= buffer[i];
                                            break;
                                        case 2:
                                            break;
                                        case 1:
                                            parser.PATSectionNumber = buffer[i];
                                            if (parser.PATSectionNumber == 0) parser.PATOffset = 0;
                                            break;
                                        case 0:
                                            parser.PATLastSectionNumber = buffer[i];
                                            parser.PATTransferState = true;
                                            break;
                                    }
                                }
                            }

                            if (parser.PacketLength == 0) parser.SyncState = false;
                        }
                        else if (parser.PID == parser.PMTPID)
                        {
                            if (parser.PMTTransferState)
                            {
                                if (bufferLength - i >= parser.PMTSectionLength)
                                    offset = parser.PMTSectionLength;
                                else
                                    offset = bufferLength - i;
                                if (parser.PacketLength <= offset) offset = parser.PacketLength;
                                if (!parser.PMT.ContainsKey(parser.PID)) parser.PMT[parser.PID] = new byte[1024];

                                var PMT = parser.PMT[parser.PID];
                                for (var k = 0; k < offset; k++)
                                {
                                    PMT[parser.PMTOffset++] = buffer[i++];
                                    --parser.PMTSectionLength;
                                    --parser.PacketLength;
                                }

                                --i;

                                if (parser.PMTSectionLength == 0)
                                {
                                    parser.PMTTransferState = false;
                                    if (parser.PMTSectionNumber == parser.PMTLastSectionNumber)
                                        //Console.WriteLine("PMT Start: " + parser.PMTTemp);
                                        try
                                        {
                                            for (var k = 0; k < parser.PMTOffset - 4; k += 5)
                                            {
                                                var streamType = PMT[k];

                                                var streamPID = (ushort) (((PMT[k + 1] & 0x1F) << 8) + PMT[k + 2]);

                                                var streamInfoLength = (ushort) (((PMT[k + 3] & 0xF) << 8) + PMT[k + 4]);

                                                /*
                                                if (streamInfoLength == 2)
                                                {
                                                    // TODO: Cleanup
                                                    //streamInfoLength = 0;
                                                }

                                                Console.WriteLine(string.Format(
                                                    "Type: {0} PID: {1} Length: {2}",
                                                    streamType, streamPID, streamInfoLength));
                                                 */

                                                if (!Streams.ContainsKey(streamPID))
                                                {
                                                    var streamDescriptors = new List<TSDescriptor>();

                                                    /*
                                                     * TODO: Getting bad streamInfoLength
                                                    if (streamInfoLength > 0)
                                                    {
                                                        for (int d = 0; d < streamInfoLength; d++)
                                                        {
                                                            byte name = PMT[k + d + 5];
                                                            byte length = PMT[k + d + 6];
                                                            TSDescriptor descriptor =
                                                                new TSDescriptor(name, length);
                                                            for (int v = 0; v < length; v++)
                                                            {
                                                                descriptor.Value[v] =
                                                                    PMT[k + d + v + 7];
                                                            }
                                                            streamDescriptors.Add(descriptor);
                                                            d += (length + 1);
                                                        }
                                                    }
                                                    */
                                                    CreateStream(streamPID, streamType, streamDescriptors);
                                                }

                                                k += streamInfoLength;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // TODO
                                            Console.WriteLine(ex.Message);
                                        }
                                }
                            }
                            else
                            {
                                --parser.PacketLength;
                                if (parser.PMTSectionStart)
                                {
                                    parser.PMTPointerField = buffer[i];
                                    if (parser.PMTPointerField == 0) parser.PMTSectionLengthParse = 3;
                                    parser.PMTSectionStart = false;
                                }
                                else if (parser.PMTPointerField > 0)
                                {
                                    --parser.PMTPointerField;
                                    if (parser.PMTPointerField == 0) parser.PMTSectionLengthParse = 3;
                                }
                                else if (parser.PMTSectionLengthParse > 0)
                                {
                                    --parser.PMTSectionLengthParse;
                                    switch (parser.PMTSectionLengthParse)
                                    {
                                        case 2:
                                            if (buffer[i] != 0x2) parser.PMTSectionLengthParse = 0;
                                            break;
                                        case 1:
                                            parser.PMTSectionLength = (ushort) ((buffer[i] & 0xF) << 8);
                                            break;
                                        case 0:
                                            parser.PMTSectionLength |= buffer[i];
                                            if (parser.PMTSectionLength > 1021)
                                                parser.PMTSectionLength = 0;
                                            else
                                                parser.PMTSectionParse = 9;
                                            break;
                                    }
                                }
                                else if (parser.PMTSectionParse > 0)
                                {
                                    --parser.PMTSectionLength;
                                    --parser.PMTSectionParse;

                                    switch (parser.PMTSectionParse)
                                    {
                                        case 8:
                                        case 7:
                                            break;
                                        case 6:
                                            parser.PMTTemp = buffer[i];
                                            break;
                                        case 5:
                                            parser.PMTSectionNumber = buffer[i];
                                            if (parser.PMTSectionNumber == 0) parser.PMTOffset = 0;
                                            break;
                                        case 4:
                                            parser.PMTLastSectionNumber = buffer[i];
                                            break;
                                        case 3:
                                            parser.PCRPID = (ushort) ((buffer[i] & 0x1F) << 8);
                                            break;
                                        case 2:
                                            parser.PCRPID |= buffer[i];
                                            break;
                                        case 1:
                                            parser.PMTProgramInfoLength = (ushort) ((buffer[i] & 0xF) << 8);
                                            break;
                                        case 0:
                                            parser.PMTProgramInfoLength |= buffer[i];
                                            if (parser.PMTProgramInfoLength == 0)
                                                parser.PMTTransferState = true;
                                            else
                                                parser.PMTProgramDescriptorLengthParse = 2;
                                            break;
                                    }
                                }
                                else if (parser.PMTProgramInfoLength > 0)
                                {
                                    --parser.PMTSectionLength;
                                    --parser.PMTProgramInfoLength;

                                    if (parser.PMTProgramDescriptorLengthParse > 0)
                                    {
                                        --parser.PMTProgramDescriptorLengthParse;
                                        switch (parser.PMTProgramDescriptorLengthParse)
                                        {
                                            case 1:
                                                parser.PMTProgramDescriptor = buffer[i];
                                                break;
                                            case 0:
                                                parser.PMTProgramDescriptorLength = buffer[i];
                                                parser.PMTProgramDescriptors.Add(new TSDescriptor(parser.PMTProgramDescriptor,
                                                    parser.PMTProgramDescriptorLength));
                                                break;
                                        }
                                    }
                                    else if (parser.PMTProgramDescriptorLength > 0)
                                    {
                                        --parser.PMTProgramDescriptorLength;

                                        var descriptor = parser.PMTProgramDescriptors[parser.PMTProgramDescriptors.Count - 1];

                                        var valueIndex = descriptor.Value.Length - parser.PMTProgramDescriptorLength - 1;

                                        descriptor.Value[valueIndex] = buffer[i];

                                        if (parser.PMTProgramDescriptorLength == 0 && parser.PMTProgramInfoLength > 0)
                                            parser.PMTProgramDescriptorLengthParse = 2;
                                    }

                                    if (parser.PMTProgramInfoLength == 0) parser.PMTTransferState = true;
                                }
                            }

                            if (parser.PacketLength == 0) parser.SyncState = false;
                        }
                        else if (parser.Stream != null && parser.StreamState != null && parser.TransportScramblingControl == 0)
                        {
                            var stream = parser.Stream;
                            var streamState = parser.StreamState;

                            streamState.Parse = (streamState.Parse << 8) + buffer[i];

                            if (streamState.TransferState)
                            {
                                if (bufferLength - i >= streamState.PacketLength && streamState.PacketLength > 0)
                                    offset = streamState.PacketLength;
                                else
                                    offset = bufferLength - i;
                                if (parser.PacketLength <= offset) offset = parser.PacketLength;
                                streamState.TransferLength = offset;

                                if (!stream.IsInitialized || stream.IsVideoStream)
                                    streamState.StreamBuffer.Add(buffer, i, offset);
                                else
                                    streamState.StreamBuffer.TransferLength += offset;

                                i += streamState.TransferLength - 1;
                                streamState.PacketLength -= streamState.TransferLength;
                                parser.PacketLength -= (byte) streamState.TransferLength;

                                streamState.TotalBytes += (ulong) streamState.TransferLength;
                                streamState.WindowBytes += (ulong) streamState.TransferLength;

                                if (streamState.PacketLength == 0)
                                {
                                    streamState.TransferState = false;
                                    streamState.TransferCount++;
                                    var isFinished = ScanStream(stream, streamState, streamState.StreamBuffer);

                                    if (!isFullScan && isFinished) return;
                                }
                            }
                            else
                            {
                                --parser.PacketLength;

                                var headerFound = false;
                                if (stream.IsVideoStream && streamState.Parse == 0x000001FD) headerFound = true;
                                if (stream.IsVideoStream && streamState.Parse >= 0x000001E0 && streamState.Parse <= 0x000001EF) headerFound = true;
                                if (stream.IsAudioStream && streamState.Parse == 0x000001BD) headerFound = true;
                                if (stream.IsAudioStream && (streamState.Parse == 0x000001FA || streamState.Parse == 0x000001FD)) headerFound = true;

                                if (!stream.IsVideoStream && !stream.IsAudioStream && (streamState.Parse == 0x000001FA || streamState.Parse == 0x000001FD ||
                                                                                       streamState.Parse == 0x000001BD ||
                                                                                       streamState.Parse >= 0x000001E0 && streamState.Parse <= 0x000001EF))
                                    headerFound = true;

                                if (headerFound)
                                {
                                    streamState.PacketLengthParse = 2;
#if DEBUG
                                    streamState.PESHeaderIndex = 0;
                                    streamState.PESHeader[streamState.PESHeaderIndex++] =
                                        (byte)((streamState.Parse >> 24) & 0xFF);
                                    streamState.PESHeader[streamState.PESHeaderIndex++] =
                                        (byte)((streamState.Parse >> 16) & 0xFF);
                                    streamState.PESHeader[streamState.PESHeaderIndex++] =
                                        (byte)((streamState.Parse >> 8) & 0xFF);
                                    streamState.PESHeader[streamState.PESHeaderIndex++] =
                                        (byte)(streamState.Parse & 0xFF);
#endif
                                }
                                else if (streamState.PacketLengthParse > 0)
                                {
                                    --streamState.PacketLengthParse;
                                    switch (streamState.PacketLengthParse)
                                    {
                                        case 1:
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 0:
                                            streamState.PacketLength = (int) (streamState.Parse & 0xFFFF);
                                            streamState.PacketParse = 3;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;
                                    }
                                }
                                else if (streamState.PacketParse > 0)
                                {
                                    --streamState.PacketLength;
                                    --streamState.PacketParse;

                                    switch (streamState.PacketParse)
                                    {
                                        case 2:
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 1:
                                            streamState.PESHeaderFlags = (byte) (streamState.Parse & 0xFF);
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 0:
                                            streamState.PESHeaderLength = (byte) (streamState.Parse & 0xFF);
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            if ((streamState.PESHeaderFlags & 0xC0) == 0x80)
                                                streamState.PTSParse = 5;
                                            else if ((streamState.PESHeaderFlags & 0xC0) == 0xC0) streamState.DTSParse = 10;
                                            if (streamState.PESHeaderLength == 0) streamState.TransferState = true;
                                            break;
                                    }
                                }
                                else if (streamState.PTSParse > 0)
                                {
                                    --streamState.PacketLength;
                                    --streamState.PESHeaderLength;
                                    --streamState.PTSParse;

                                    switch (streamState.PTSParse)
                                    {
                                        case 4:
                                            streamState.PTSTemp = (streamState.Parse & 0xE) << 29;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            break;

                                        case 3:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFF) << 22;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 2:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFE) << 14;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 1:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFF) << 7;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 0:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFE) >> 1;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            streamState.PTS = streamState.PTSTemp;

                                            if (streamState.PTS > streamState.PTSLast)
                                            {
                                                if (streamState.PTSLast > 0) streamState.PTSTransfer = streamState.PTS - streamState.PTSLast;
                                                streamState.PTSLast = streamState.PTS;
                                            }

                                            streamState.PTSDiff = streamState.PTS - streamState.DTSPrev;

                                            if (streamState.PTSCount > 0 && stream.IsVideoStream)
                                            {
                                                UpdateStreamBitrates(stream.PID, streamState.PTS, streamState.PTSDiff);
                                                if (streamState.DTSTemp < parser.PTSFirst) parser.PTSFirst = streamState.DTSTemp;
                                                if (streamState.DTSTemp > parser.PTSLast) parser.PTSLast = streamState.DTSTemp;
                                                Length = (double) (parser.PTSLast - parser.PTSFirst) / 90000;
                                            }

                                            streamState.DTSPrev = streamState.PTS;
                                            streamState.PTSCount++;
                                            if (streamState.PESHeaderLength == 0) streamState.TransferState = true;
                                            break;
                                    }
                                }
                                else if (streamState.DTSParse > 0)
                                {
                                    --streamState.PacketLength;
                                    --streamState.PESHeaderLength;
                                    --streamState.DTSParse;

                                    switch (streamState.DTSParse)
                                    {
                                        case 9:
                                            streamState.PTSTemp = (streamState.Parse & 0xE) << 29;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 8:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFF) << 22;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 7:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFE) << 14;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            break;

                                        case 6:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFF) << 7;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 5:
                                            streamState.PTSTemp |= (streamState.Parse & 0xFE) >> 1;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            streamState.PTS = streamState.PTSTemp;
                                            if (streamState.PTS > streamState.PTSLast) streamState.PTSLast = streamState.PTS;
                                            break;

                                        case 4:
                                            streamState.DTSTemp = (streamState.Parse & 0xE) << 29;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            break;

                                        case 3:
                                            streamState.DTSTemp |= (streamState.Parse & 0xFF) << 22;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            break;

                                        case 2:
                                            streamState.DTSTemp |= (streamState.Parse & 0xFE) << 14;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            break;

                                        case 1:
                                            streamState.DTSTemp |= (streamState.Parse & 0xFF) << 7;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xFF);
#endif
                                            break;

                                        case 0:
                                            streamState.DTSTemp |= (streamState.Parse & 0xFE) >> 1;
#if DEBUG
                                            streamState.PESHeader[streamState.PESHeaderIndex++] =
                                                (byte)(streamState.Parse & 0xff);
#endif
                                            streamState.PTSDiff = streamState.DTSTemp - streamState.DTSPrev;

                                            if (streamState.PTSCount > 0 && stream.IsVideoStream)
                                            {
                                                UpdateStreamBitrates(stream.PID, streamState.DTSTemp, streamState.PTSDiff);
                                                if (streamState.DTSTemp < parser.PTSFirst) parser.PTSFirst = streamState.DTSTemp;
                                                if (streamState.DTSTemp > parser.PTSLast) parser.PTSLast = streamState.DTSTemp;
                                                Length = (double) (parser.PTSLast - parser.PTSFirst) / 90000;
                                            }

                                            streamState.DTSPrev = streamState.DTSTemp;
                                            streamState.PTSCount++;
                                            if (streamState.PESHeaderLength == 0) streamState.TransferState = true;
                                            break;
                                    }
                                }
                                else if (streamState.PESHeaderLength > 0)
                                {
                                    --streamState.PacketLength;
                                    --streamState.PESHeaderLength;
#if DEBUG
                                    streamState.PESHeader[streamState.PESHeaderIndex++] =
                                        (byte)(streamState.Parse & 0xFF);
#endif
                                    if (streamState.PESHeaderLength == 0) streamState.TransferState = true;
                                }
                            }

                            if (parser.PacketLength == 0) parser.SyncState = false;
                        }
                        else
                        {
                            parser.PacketLength--;
                            if (bufferLength - i >= parser.PacketLength)
                            {
                                i = i + parser.PacketLength;
                                parser.PacketLength = 0;
                            }
                            else
                            {
                                parser.PacketLength -= (byte) (bufferLength - i + 1);
                                i = bufferLength;
                            }

                            if (parser.PacketLength == 0) parser.SyncState = false;
                        }

                    Size += bufferLength;
                }

                ulong PTSLast = 0;
                ulong PTSDiff = 0;
                foreach (var stream in Streams.Values)
                {
                    if (!stream.IsVideoStream) continue;

                    if (_streamStates.ContainsKey(stream.PID) && _streamStates[stream.PID].PTSLast > PTSLast)
                    {
                        PTSLast = _streamStates[stream.PID].PTSLast;
                        PTSDiff = PTSLast - _streamStates[stream.PID].DTSPrev;
                    }

                    UpdateStreamBitrates(stream.PID, PTSLast, PTSDiff);
                }
            }
            finally
            {
                if (fileStream != null) fileStream.Close();
            }
        }

        private TSStream CreateStream(ushort streamPID, byte streamType, List<TSDescriptor> streamDescriptors)
        {
            TSStream stream = null;

            switch ((TSStreamType) streamType)
            {
                case TSStreamType.MVC_VIDEO:
                case TSStreamType.AVC_VIDEO:
                case TSStreamType.HEVC_VIDEO:
                case TSStreamType.MPEG1_VIDEO:
                case TSStreamType.MPEG2_VIDEO:
                case TSStreamType.VC1_VIDEO:
                {
                    stream = new TSVideoStream();
                }
                    break;

                case TSStreamType.AC3_AUDIO:
                case TSStreamType.AC3_PLUS_AUDIO:
                case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                case TSStreamType.AC3_TRUE_HD_AUDIO:
                case TSStreamType.DTS_AUDIO:
                case TSStreamType.DTS_HD_AUDIO:
                case TSStreamType.DTS_HD_MASTER_AUDIO:
                case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                case TSStreamType.LPCM_AUDIO:
                case TSStreamType.MPEG1_AUDIO:
                case TSStreamType.MPEG2_AUDIO:
                {
                    stream = new TSAudioStream();
                }
                    break;

                case TSStreamType.INTERACTIVE_GRAPHICS:
                case TSStreamType.PRESENTATION_GRAPHICS:
                {
                    stream = new TSGraphicsStream();
                }
                    break;

                case TSStreamType.SUBTITLE:
                {
                    stream = new TSTextStream();
                }
                    break;
            }

            if (stream != null && !Streams.ContainsKey(streamPID))
            {
                stream.PID = streamPID;
                stream.StreamType = (TSStreamType) streamType;
                stream.Descriptors = streamDescriptors;
                Streams[stream.PID] = stream;
            }

            if (!StreamDiagnostics.ContainsKey(streamPID)) StreamDiagnostics[streamPID] = new List<TSStreamDiagnostics>();

            return stream;
        }
    }
}