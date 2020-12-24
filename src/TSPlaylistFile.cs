#undef DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Udf;

namespace BDInfo
{
    public class TSPlaylistFile
    {
        private readonly UdfReader _cdReader;
        private readonly DiscFileInfo _dFileInfo;
        private readonly FileInfo _fileInfo;
        private readonly bool _isCustom;
        private readonly Dictionary<ushort, TSStream> _playlistStreams = new();
       
        private string _fileType;
        private bool _hasLoops;
        private bool _isInitialized;
        private BDROM _bdrom;
        
        public readonly List<Dictionary<double, TSStreamClip>> AngleClips = new();
        public readonly List<Dictionary<ushort, TSStream>> AngleStreams = new();
        public readonly List<TSAudioStream> AudioStreams = new();
        public readonly List<double> Chapters = new();
        public readonly List<TSGraphicsStream> GraphicsStreams = new();
        public readonly List<TSTextStream> TextStreams = new();
        public readonly List<TSVideoStream> VideoStreams = new();
        public readonly string Name;
        public readonly List<TSStream> SortedStreams = new();
        public readonly List<TSStreamClip> StreamClips = new();
        public readonly Dictionary<ushort, TSStream> Streams = new();
        public int AngleCount;
        public bool HasHiddenTracks;
        public bool MVCBaseViewR;

        public TSPlaylistFile(BDROM bdrom, FileInfo fileInfo)
        {
            _bdrom = bdrom;
            _fileInfo = fileInfo;
            _dFileInfo = null;
            _cdReader = null;
            Name = fileInfo.Name.ToUpper();
        }

        public TSPlaylistFile(BDROM bdrom, DiscFileInfo fileInfo, UdfReader reader)
        {
            _bdrom = bdrom;
            _dFileInfo = fileInfo;
            _fileInfo = null;
            _cdReader = reader;
            Name = fileInfo.Name.ToUpper();
        }

        public TSPlaylistFile(BDROM bdrom, string name, List<TSStreamClip> clips)
        {
            _bdrom = bdrom;
            Name = name;
            _isCustom = true;
            foreach (var clip in clips)
            {
                var newClip = new TSStreamClip(clip.StreamFile, clip.StreamClipFile);

                newClip.Name = clip.Name;
                newClip.TimeIn = clip.TimeIn;
                newClip.TimeOut = clip.TimeOut;
                newClip.Length = newClip.TimeOut - newClip.TimeIn;
                newClip.RelativeTimeIn = TotalLength;
                newClip.RelativeTimeOut = newClip.RelativeTimeIn + newClip.Length;
                newClip.AngleIndex = clip.AngleIndex;
                //newClip.Chapters.Add(clip.TimeIn);
                StreamClips.Add(newClip);

                if (newClip.AngleIndex > AngleCount) AngleCount = newClip.AngleIndex;
                if (newClip.AngleIndex == 0) Chapters.Add(newClip.RelativeTimeIn);
            }

            LoadStreamClips();
            _isInitialized = true;
        }

        public ulong InterleavedFileSize
        {
            get
            {
                ulong size = 0;
                foreach (var clip in StreamClips) size += clip.InterleavedFileSize;
                return size;
            }
        }

        public ulong FileSize
        {
            get
            {
                return StreamClips.Aggregate<TSStreamClip, ulong>(0, (current, clip) => current + clip.FileSize);
            }
        }

        public double TotalLength
        {
            get
            {
                double length = 0;
                foreach (var clip in StreamClips)
                    if (clip.AngleIndex == 0)
                        length += clip.Length;
                return length;
            }
        }

        public double TotalAngleLength
        {
            get
            {
                double length = 0;
                foreach (var clip in StreamClips) length += clip.Length;
                return length;
            }
        }

        public ulong TotalSize
        {
            get
            {
                ulong size = 0;
                foreach (var clip in StreamClips)
                    if (clip.AngleIndex == 0)
                        size += clip.PacketSize;
                return size;
            }
        }

        public ulong TotalAngleSize
        {
            get
            {
                ulong size = 0;
                foreach (var clip in StreamClips) size += clip.PacketSize;
                return size;
            }
        }

        public ulong TotalBitRate
        {
            get
            {
                if (TotalLength > 0) return (ulong) Math.Round(TotalSize * 8.0 / TotalLength);
                return 0;
            }
        }

        public ulong TotalAngleBitRate
        {
            get
            {
                if (TotalAngleLength > 0) return (ulong) Math.Round(TotalAngleSize * 8.0 / TotalAngleLength);
                return 0;
            }
        }

        public bool IsValid
        {
            get
            {
                if (!_isInitialized) return false;

                if (BDInfoSettings.FilterShortPlaylists && TotalLength < BDInfoSettings.FilterShortPlaylistsValue) return false;

                if (_hasLoops && BDInfoSettings.FilterLoopingPlaylists) return false;

                return true;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public string GetFilePath()
        {
            if (!string.IsNullOrEmpty(_fileInfo?.FullName)) return _fileInfo.FullName;

            if (!string.IsNullOrEmpty(_dFileInfo?.FullName)) return _dFileInfo.FullName;

            return string.Empty;
        }

        public void Scan(Dictionary<string, TSStreamFile> streamFiles, Dictionary<string, TSStreamClipFile> streamClipFiles)
        {
            FileStream fileStream = null;
            Stream discFileStream = null;
            BinaryReader fileReader = null;

            try
            {
                Streams.Clear();
                StreamClips.Clear();

                ulong streamLength = 0;
                if (_fileInfo != null)
                {
                    fileStream = File.OpenRead(_fileInfo.FullName);
                    fileReader = new BinaryReader(fileStream);
                    streamLength = (ulong) fileStream.Length;
                }
                else
                {
                    _cdReader.OpenFile(_dFileInfo.FullName, FileMode.Open);
                    discFileStream = _cdReader.GetFileInfo(_dFileInfo.FullName).OpenRead();
                    fileReader = new BinaryReader(discFileStream);
                    streamLength = (ulong) discFileStream.Length;
                }

                var data = new byte[streamLength];
                var dataLength = fileReader.Read(data, 0, data.Length);

                var pos = 0;

                _fileType = ToolBox.ReadString(data, 8, ref pos);
                if (_fileType != "MPLS0100" && _fileType != "MPLS0200" && _fileType != "MPLS0300")
                    throw new Exception($"Playlist {_fileInfo.Name} has an unknown file type {_fileType}.");

                var playlistOffset = ReadInt32(data, ref pos);
                var chaptersOffset = ReadInt32(data, ref pos);
                var extensionsOffset = ReadInt32(data, ref pos);

                // misc flags
                pos = 0x38;
                var miscFlags = ReadByte(data, ref pos);

                // MVC_Base_view_R_flag is stored in 4th bit
                MVCBaseViewR = (miscFlags & 0x10) != 0;

                pos = playlistOffset;

                var playlistLength = ReadInt32(data, ref pos);
                var playlistReserved = ReadInt16(data, ref pos);
                var itemCount = ReadInt16(data, ref pos);
                ReadInt16(data, ref pos);

                var chapterClips = new List<TSStreamClip>();
                for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
                {
                    var itemStart = pos;
                    var itemLength = ReadInt16(data, ref pos);
                    var itemName = ToolBox.ReadString(data, 5, ref pos);
                    var itemType = ToolBox.ReadString(data, 4, ref pos);

                    TSStreamFile streamFile = null;
                    var streamFileName = $"{itemName}.M2TS";
                    if (streamFiles.ContainsKey(streamFileName)) streamFile = streamFiles[streamFileName];
                    if (streamFile == null) Debug.WriteLine($"Playlist {_fileInfo.Name} referenced missing file {streamFileName}.");

                    TSStreamClipFile streamClipFile = null;
                    var streamClipFileName = $"{itemName}.CLPI";
                    if (streamClipFiles.ContainsKey(streamClipFileName)) streamClipFile = streamClipFiles[streamClipFileName];
                    if (streamClipFile == null) throw new Exception($"Playlist {_fileInfo.Name} referenced missing file {streamFileName}.");

                    pos += 1;
                    var multiangle = (data[pos] >> 4) & 0x01;
                    var condition = data[pos] & 0x0F;
                    pos += 2;

                    var inTime = ReadInt32(data, ref pos);
                    if (inTime < 0) inTime &= 0x7FFFFFFF;
                    var timeIn = (double) inTime / 45000;

                    var outTime = ReadInt32(data, ref pos);
                    if (outTime < 0) outTime &= 0x7FFFFFFF;
                    var timeOut = (double) outTime / 45000;

                    var streamClip = new TSStreamClip(streamFile, streamClipFile);

                    streamClip.Name = streamFileName; //TODO
                    streamClip.TimeIn = timeIn;
                    streamClip.TimeOut = timeOut;
                    streamClip.Length = streamClip.TimeOut - streamClip.TimeIn;
                    streamClip.RelativeTimeIn = TotalLength;
                    streamClip.RelativeTimeOut = streamClip.RelativeTimeIn + streamClip.Length;
                    StreamClips.Add(streamClip);
                    chapterClips.Add(streamClip);

                    pos += 12;
                    if (multiangle > 0)
                    {
                        int angles = data[pos];
                        pos += 2;
                        for (var angle = 0; angle < angles - 1; angle++)
                        {
                            var angleName = ToolBox.ReadString(data, 5, ref pos);
                            var angleType = ToolBox.ReadString(data, 4, ref pos);
                            pos += 1;

                            TSStreamFile angleFile = null;
                            var angleFileName = $"{angleName}.M2TS";
                            if (streamFiles.ContainsKey(angleFileName)) angleFile = streamFiles[angleFileName];
                            if (angleFile == null) throw new Exception($"Playlist {_fileInfo.Name} referenced missing angle file {angleFileName}.");

                            TSStreamClipFile angleClipFile = null;
                            var angleClipFileName = $"{angleName}.CLPI";
                            if (streamClipFiles.ContainsKey(angleClipFileName)) angleClipFile = streamClipFiles[angleClipFileName];
                            if (angleClipFile == null) throw new Exception($"Playlist {_fileInfo.Name} referenced missing angle file {angleClipFileName}.");

                            var angleClip = new TSStreamClip(angleFile, angleClipFile)
                            {
                                AngleIndex = angle + 1,
                                TimeIn = streamClip.TimeIn,
                                TimeOut = streamClip.TimeOut,
                                RelativeTimeIn = streamClip.RelativeTimeIn,
                                RelativeTimeOut = streamClip.RelativeTimeOut,
                                Length = streamClip.Length
                            };
                            StreamClips.Add(angleClip);
                        }

                        if (angles - 1 > AngleCount) AngleCount = angles - 1;
                    }

                    var streamInfoLength = ReadInt16(data, ref pos);
                    pos += 2;
                    int streamCountVideo = data[pos++];
                    int streamCountAudio = data[pos++];
                    int streamCountPG = data[pos++];
                    int streamCountIG = data[pos++];
                    int streamCountSecondaryAudio = data[pos++];
                    int streamCountSecondaryVideo = data[pos++];
                    int streamCountPIP = data[pos++];
                    pos += 5;

#if DEBUG
                    Debug.WriteLine(string.Format(
                        "{0} : {1} -> V:{2} A:{3} PG:{4} IG:{5} 2A:{6} 2V:{7} PIP:{8}", 
                        Name, streamFileName, streamCountVideo, streamCountAudio, streamCountPG, streamCountIG, 
                        streamCountSecondaryAudio, streamCountSecondaryVideo, streamCountPIP));
#endif

                    for (var i = 0; i < streamCountVideo; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                    }

                    for (var i = 0; i < streamCountAudio; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                    }

                    for (var i = 0; i < streamCountPG; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                    }

                    for (var i = 0; i < streamCountIG; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                    }

                    for (var i = 0; i < streamCountSecondaryAudio; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                        pos += 2;
                    }

                    for (var i = 0; i < streamCountSecondaryVideo; i++)
                    {
                        var stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) _playlistStreams[stream.PID] = stream;
                        pos += 6;
                    }
                    /*
                     * TODO
                     * 
                    for (int i = 0; i < streamCountPIP; i++)
                    {
                        TSStream stream = CreatePlaylistStream(data, ref pos);
                        if (stream != null) PlaylistStreams[stream.PID] = stream;
                    }
                    */

                    pos += itemLength - (pos - itemStart) + 2;
                }

                pos = chaptersOffset + 4;

                var chapterCount = ReadInt16(data, ref pos);

                for (var chapterIndex = 0; chapterIndex < chapterCount; chapterIndex++)
                {
                    int chapterType = data[pos + 1];

                    if (chapterType == 1)
                    {
                        var streamFileIndex = (data[pos + 2] << 8) + data[pos + 3];

                        var chapterTime = ((long) data[pos + 4] << 24) + ((long) data[pos + 5] << 16) + ((long) data[pos + 6] << 8) + data[pos + 7];

                        var streamClip = chapterClips[streamFileIndex];

                        var chapterSeconds = (double) chapterTime / 45000;

                        var relativeSeconds = chapterSeconds - streamClip.TimeIn + streamClip.RelativeTimeIn;

                        // TODO: Ignore short last chapter?
                        if (TotalLength - relativeSeconds > 1.0)
                            //streamClip.Chapters.Add(chapterSeconds);
                            Chapters.Add(relativeSeconds);
                    }

                    pos += 14;
                }
            }
            finally
            {
                if (fileReader != null) fileReader.Close();
                if (fileStream != null) fileStream.Close();
                if (discFileStream != null) discFileStream.Close();
            }
        }

        public void Initialize()
        {
            LoadStreamClips();

            var clipTimes = new Dictionary<string, List<double>>();
            foreach (var clip in StreamClips)
                if (clip.AngleIndex == 0)
                {
                    if (clipTimes.ContainsKey(clip.Name))
                    {
                        if (clipTimes[clip.Name].Contains(clip.TimeIn))
                        {
                            _hasLoops = true;
                            break;
                        }

                        clipTimes[clip.Name].Add(clip.TimeIn);
                    }
                    else
                    {
                        clipTimes[clip.Name] = new List<double> {clip.TimeIn};
                    }
                }

            ClearBitrates();
            _isInitialized = true;
        }

        protected TSStream CreatePlaylistStream(byte[] data, ref int pos)
        {
            TSStream stream = null;

            var start = pos;

            int headerLength = data[pos++];
            var headerPos = pos;
            int headerType = data[pos++];

            var pid = 0;
            var subpathid = 0;
            var subclipid = 0;

            switch (headerType)
            {
                case 1:
                    pid = ReadInt16(data, ref pos);
                    break;
                case 2:
                    subpathid = data[pos++];
                    subclipid = data[pos++];
                    pid = ReadInt16(data, ref pos);
                    break;
                case 3:
                    subpathid = data[pos++];
                    pid = ReadInt16(data, ref pos);
                    break;
                case 4:
                    subpathid = data[pos++];
                    subclipid = data[pos++];
                    pid = ReadInt16(data, ref pos);
                    break;
            }

            pos = headerPos + headerLength;

            int streamLength = data[pos++];
            var streamPos = pos;

            var streamType = (TSStreamType) data[pos++];
            switch (streamType)
            {
                case TSStreamType.MVC_VIDEO:
                    // TODO
                    break;

                case TSStreamType.HEVC_VIDEO:
                case TSStreamType.AVC_VIDEO:
                case TSStreamType.MPEG1_VIDEO:
                case TSStreamType.MPEG2_VIDEO:
                case TSStreamType.VC1_VIDEO:

                    var videoFormat = (TSVideoFormat) (data[pos] >> 4);
                    var frameRate = (TSFrameRate) (data[pos] & 0xF);
                    var aspectRatio = (TSAspectRatio) (data[pos + 1] >> 4);

                    stream = new TSVideoStream();
                    ((TSVideoStream) stream).VideoFormat = videoFormat;
                    ((TSVideoStream) stream).AspectRatio = aspectRatio;
                    ((TSVideoStream) stream).FrameRate = frameRate;

#if DEBUG
                            Debug.WriteLine(string.Format(
                                "\t{0} {1} {2} {3} {4}",
                                pid,
                                streamType,
                                videoFormat,
                                frameRate,
                                aspectRatio));
#endif

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

                    int audioFormat = ReadByte(data, ref pos);

                    var channelLayout = (TSChannelLayout) (audioFormat >> 4);
                    var sampleRate = (TSSampleRate) (audioFormat & 0xF);

                    var audioLanguage = ToolBox.ReadString(data, 3, ref pos);

                    stream = new TSAudioStream();
                    ((TSAudioStream) stream).ChannelLayout = channelLayout;
                    ((TSAudioStream) stream).SampleRate = TSAudioStream.ConvertSampleRate(sampleRate);
                    ((TSAudioStream) stream).LanguageCode = audioLanguage;

#if DEBUG
                    Debug.WriteLine(string.Format(
                        "\t{0} {1} {2} {3} {4}",
                        pid,
                        streamType,
                        audioLanguage,
                        channelLayout,
                        sampleRate));
#endif

                    break;

                case TSStreamType.INTERACTIVE_GRAPHICS:
                case TSStreamType.PRESENTATION_GRAPHICS:

                    var graphicsLanguage = ToolBox.ReadString(data, 3, ref pos);

                    stream = new TSGraphicsStream();
                    ((TSGraphicsStream) stream).LanguageCode = graphicsLanguage;

                    if (data[pos] != 0)
                    {
                    }

#if DEBUG
                    Debug.WriteLine(string.Format(
                        "\t{0} {1} {2}",
                        pid,
                        streamType,
                        graphicsLanguage));
#endif

                    break;

                case TSStreamType.SUBTITLE:

                    int code = ReadByte(data, ref pos); // TODO
                    var textLanguage = ToolBox.ReadString(data, 3, ref pos);

                    stream = new TSTextStream();
                    ((TSTextStream) stream).LanguageCode = textLanguage;

#if DEBUG
                    Debug.WriteLine(string.Format(
                        "\t{0} {1} {2}",
                        pid,
                        streamType,
                        textLanguage));
#endif

                    break;
            }

            pos = streamPos + streamLength;

            if (stream != null)
            {
                stream.PID = (ushort) pid;
                stream.StreamType = streamType;
            }

            return stream;
        }

        private void LoadStreamClips()
        {
            AngleClips.Clear();
            if (AngleCount > 0)
                for (var angleIndex = 0; angleIndex < AngleCount; angleIndex++)
                    AngleClips.Add(new Dictionary<double, TSStreamClip>());

            TSStreamClip referenceClip = null;
            if (StreamClips.Count > 0) referenceClip = StreamClips[0];
            foreach (var clip in StreamClips)
            {
                if (clip.StreamClipFile.Streams.Count > referenceClip.StreamClipFile.Streams.Count)
                    referenceClip = clip;
                else if (clip.Length > referenceClip.Length) referenceClip = clip;
                if (AngleCount > 0)
                {
                    if (clip.AngleIndex == 0)
                        for (var angleIndex = 0; angleIndex < AngleCount; angleIndex++)
                            AngleClips[angleIndex][clip.RelativeTimeIn] = clip;
                    else
                        AngleClips[clip.AngleIndex - 1][clip.RelativeTimeIn] = clip;
                }
            }

            foreach (var clipStream in referenceClip.StreamClipFile.Streams.Values)
                if (!Streams.ContainsKey(clipStream.PID))
                {
                    var stream = clipStream.Clone();
                    Streams[clipStream.PID] = stream;

                    if (!_isCustom && !_playlistStreams.ContainsKey(stream.PID))
                    {
                        stream.IsHidden = true;
                        HasHiddenTracks = true;
                    }

                    if (stream.IsVideoStream)
                        VideoStreams.Add((TSVideoStream) stream);
                    else if (stream.IsAudioStream)
                        AudioStreams.Add((TSAudioStream) stream);
                    else if (stream.IsGraphicsStream)
                        GraphicsStreams.Add((TSGraphicsStream) stream);
                    else if (stream.IsTextStream) TextStreams.Add((TSTextStream) stream);
                }

            if (referenceClip.StreamFile != null)
            {
                // TODO: Better way to add this in?
                if (BDInfoSettings.EnableSSIF && referenceClip.StreamFile.InterleavedFile != null && referenceClip.StreamFile.Streams.ContainsKey(4114) &&
                    !Streams.ContainsKey(4114))
                {
                    var stream = referenceClip.StreamFile.Streams[4114].Clone();
                    Streams[4114] = stream;
                    if (stream.IsVideoStream) VideoStreams.Add((TSVideoStream) stream);
                }

                foreach (var clipStream in referenceClip.StreamFile.Streams.Values)
                    if (Streams.ContainsKey(clipStream.PID))
                    {
                        var stream = Streams[clipStream.PID];

                        if (stream.StreamType != clipStream.StreamType) continue;

                        if (clipStream.BitRate > stream.BitRate) stream.BitRate = clipStream.BitRate;
                        stream.IsVBR = clipStream.IsVBR;

                        if (stream.IsVideoStream && clipStream.IsVideoStream)
                        {
                            ((TSVideoStream) stream).EncodingProfile = ((TSVideoStream) clipStream).EncodingProfile;
                            ((TSVideoStream) stream).ExtendedData = ((TSVideoStream) clipStream).ExtendedData;
                        }
                        else if (stream.IsAudioStream && clipStream.IsAudioStream)
                        {
                            var audioStream = (TSAudioStream) stream;
                            var clipAudioStream = (TSAudioStream) clipStream;

                            if (clipAudioStream.ChannelCount > audioStream.ChannelCount) audioStream.ChannelCount = clipAudioStream.ChannelCount;
                            if (clipAudioStream.LFE > audioStream.LFE) audioStream.LFE = clipAudioStream.LFE;
                            if (clipAudioStream.SampleRate > audioStream.SampleRate) audioStream.SampleRate = clipAudioStream.SampleRate;
                            if (clipAudioStream.BitDepth > audioStream.BitDepth) audioStream.BitDepth = clipAudioStream.BitDepth;
                            if (clipAudioStream.DialNorm < audioStream.DialNorm) audioStream.DialNorm = clipAudioStream.DialNorm;
                            if (clipAudioStream.AudioMode != TSAudioMode.Unknown) audioStream.AudioMode = clipAudioStream.AudioMode;
                            if (clipAudioStream.HasExtensions != audioStream.HasExtensions) audioStream.HasExtensions = clipAudioStream.HasExtensions;
                            if (clipAudioStream.CoreStream != null && audioStream.CoreStream == null)
                                audioStream.CoreStream = (TSAudioStream) clipAudioStream.CoreStream.Clone();
                        }
                    }
            }

            for (var i = 0; i < AngleCount; i++) AngleStreams.Add(new Dictionary<ushort, TSStream>());

            if (!BDInfoSettings.KeepStreamOrder) VideoStreams.Sort(CompareVideoStreams);
            foreach (var stream in VideoStreams)
            {
                SortedStreams.Add(stream);
                for (var i = 0; i < AngleCount; i++)
                {
                    var angleStream = stream.Clone();
                    angleStream.AngleIndex = i + 1;
                    AngleStreams[i][angleStream.PID] = angleStream;
                    SortedStreams.Add(angleStream);
                }
            }

            if (!BDInfoSettings.KeepStreamOrder) AudioStreams.Sort(CompareAudioStreams);
            foreach (var stream in AudioStreams) SortedStreams.Add(stream);

            if (!BDInfoSettings.KeepStreamOrder) GraphicsStreams.Sort(CompareGraphicsStreams);
            foreach (var stream in GraphicsStreams) SortedStreams.Add(stream);

            if (!BDInfoSettings.KeepStreamOrder) TextStreams.Sort(CompareTextStreams);
            foreach (var stream in TextStreams) SortedStreams.Add(stream);
        }

        public void ClearBitrates()
        {
            foreach (var clip in StreamClips)
            {
                clip.PayloadBytes = 0;
                clip.PacketCount = 0;
                clip.PacketSeconds = 0;

                if (clip.StreamFile != null)
                {
                    foreach (var stream in clip.StreamFile.Streams.Values)
                    {
                        stream.PayloadBytes = 0;
                        stream.PacketCount = 0;
                        stream.PacketSeconds = 0;
                    }

                    clip.StreamFile?.StreamDiagnostics?.Clear();
                }
            }

            foreach (var stream in SortedStreams)
            {
                stream.PayloadBytes = 0;
                stream.PacketCount = 0;
                stream.PacketSeconds = 0;
            }
        }

        public static int CompareVideoStreams(TSVideoStream x, TSVideoStream y)
        {
            if (x == null && y == null) return 0;

            if (x == null && y != null) return 1;

            if (x != null && y == null) return -1;

            if (x.Height > y.Height) return -1;
            if (y.Height > x.Height) return 1;
            if (x.PID > y.PID) return 1;
            if (y.PID > x.PID) return -1;
            return 0;
        }

        public static int CompareAudioStreams(TSAudioStream x, TSAudioStream y)
        {
            if (x == y) return 0;

            if (x == null && y == null) return 0;

            if (x == null && y != null) return -1;

            if (x != null && y == null) return 1;

            if (x.ChannelCount > y.ChannelCount) return -1;

            if (y.ChannelCount > x.ChannelCount) return 1;

            var sortX = GetStreamTypeSortIndex(x.StreamType);
            var sortY = GetStreamTypeSortIndex(y.StreamType);

            if (sortX > sortY) return -1;

            if (sortY > sortX) return 1;

            if (x.LanguageCode == "eng") return -1;
            if (y.LanguageCode == "eng") return 1;
            if (x.LanguageCode != y.LanguageCode) return string.Compare(x.LanguageName, y.LanguageName);
            if (x.PID < y.PID) return -1;
            if (y.PID < x.PID) return 1;
            return 0;
        }

        public static int CompareTextStreams(TSTextStream x, TSTextStream y)
        {
            if (x == y) return 0;

            if (x == null && y == null) return 0;

            if (x == null && y != null) return -1;

            if (x != null && y == null) return 1;

            if (x.LanguageCode == "eng") return -1;

            if (y.LanguageCode == "eng") return 1;

            if (x.LanguageCode == y.LanguageCode)
            {
                if (x.PID > y.PID) return 1;
                if (y.PID > x.PID) return -1;
                return 0;
            }

            return string.Compare(x.LanguageName, y.LanguageName);
        }

        private static int CompareGraphicsStreams(TSGraphicsStream x, TSGraphicsStream y)
        {
            if (x == y) return 0;

            if (x == null && y == null) return 0;

            if (x == null && y != null) return -1;

            if (x != null && y == null) return 1;

            var sortX = GetStreamTypeSortIndex(x.StreamType);
            var sortY = GetStreamTypeSortIndex(y.StreamType);

            if (sortX > sortY) return -1;

            if (sortY > sortX) return 1;

            if (x.LanguageCode == "eng") return -1;

            if (y.LanguageCode == "eng") return 1;

            if (x.LanguageCode == y.LanguageCode)
            {
                if (x.PID > y.PID) return 1;
                if (y.PID > x.PID) return -1;
                return 0;
            }

            return string.Compare(x.LanguageName, y.LanguageName);
        }

        private static int GetStreamTypeSortIndex(TSStreamType streamType)
        {
            switch (streamType)
            {
                case TSStreamType.Unknown:
                    return 0;
                case TSStreamType.MPEG1_VIDEO:
                    return 1;
                case TSStreamType.MPEG2_VIDEO:
                    return 2;
                case TSStreamType.AVC_VIDEO:
                    return 3;
                case TSStreamType.VC1_VIDEO:
                    return 4;
                case TSStreamType.MVC_VIDEO:
                    return 5;
                case TSStreamType.HEVC_VIDEO:
                    return 6;

                case TSStreamType.MPEG1_AUDIO:
                    return 1;
                case TSStreamType.MPEG2_AUDIO:
                    return 2;
                case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                    return 3;
                case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                    return 4;
                case TSStreamType.AC3_AUDIO:
                    return 5;
                case TSStreamType.DTS_AUDIO:
                    return 6;
                case TSStreamType.AC3_PLUS_AUDIO:
                    return 7;
                case TSStreamType.DTS_HD_AUDIO:
                    return 8;
                case TSStreamType.AC3_TRUE_HD_AUDIO:
                    return 9;
                case TSStreamType.DTS_HD_MASTER_AUDIO:
                    return 10;
                case TSStreamType.LPCM_AUDIO:
                    return 11;

                case TSStreamType.SUBTITLE:
                    return 1;
                case TSStreamType.INTERACTIVE_GRAPHICS:
                    return 2;
                case TSStreamType.PRESENTATION_GRAPHICS:
                    return 3;

                default:
                    return 0;
            }
        }

        protected int ReadInt32(byte[] data, ref int pos)
        {
            var val = (data[pos] << 24) + (data[pos + 1] << 16) + (data[pos + 2] << 8) + data[pos + 3];

            pos += 4;

            return val;
        }

        protected int ReadInt16(byte[] data, ref int pos)
        {
            var val = (data[pos] << 8) + data[pos + 1];

            pos += 2;

            return val;
        }

        protected byte ReadByte(byte[] data, ref int pos)
        {
            return data[pos++];
        }
    }
}