#undef DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscUtils;
using DiscUtils.Udf;

namespace BDInfo
{
    public class TSStreamClipFile
    {
        private readonly UdfReader _cdReader;
        private readonly DiscFileInfo _dFileInfo;

        private readonly FileInfo _fileInfo;
        private string _fileType;
        public readonly string Name;

        public readonly Dictionary<ushort, TSStream> Streams = new();

        public TSStreamClipFile(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            _dFileInfo = null;
            _cdReader = null;
            Name = fileInfo.Name.ToUpper();
        }

        public TSStreamClipFile(DiscFileInfo fileInfo, UdfReader reader)
        {
            _dFileInfo = fileInfo;
            _fileInfo = null;
            _cdReader = reader;
            Name = fileInfo.Name.ToUpper();
        }

        public void Scan()
        {
            FileStream fileStream = null;
            Stream discFileStream = null;
            BinaryReader fileReader = null;

            try
            {
#if DEBUG
                Debug.WriteLine(string.Format(
                    "Scanning {0}...", Name));
#endif
                Streams.Clear();

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
                fileReader.Read(data, 0, data.Length);

                var fileType = new byte[8];
                Array.Copy(data, 0, fileType, 0, fileType.Length);

                _fileType = Encoding.ASCII.GetString(fileType);
                if (_fileType != "HDMV0100" && _fileType != "HDMV0200" && _fileType != "HDMV0300")
                    throw new Exception($"Clip info file {_fileInfo.Name} has an unknown file type {_fileType}.");
#if DEBUG
                Debug.WriteLine(string.Format(
                    "\tFileType: {0}", FileType));
#endif
                var clipIndex = (data[12] << 24) + (data[13] << 16) + (data[14] << 8) + data[15];

                var clipLength = (data[clipIndex] << 24) + (data[clipIndex + 1] << 16) + (data[clipIndex + 2] << 8) + data[clipIndex + 3];

                var clipData = new byte[clipLength];
                Array.Copy(data, clipIndex + 4, clipData, 0, clipData.Length);

                int streamCount = clipData[8];
#if DEBUG
                Debug.WriteLine(string.Format(
                    "\tStreamCount: {0}", streamCount));
#endif
                var streamOffset = 10;
                for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
                {
                    TSStream stream = null;

                    var PID = (ushort) ((clipData[streamOffset] << 8) + clipData[streamOffset + 1]);

                    streamOffset += 2;

                    var streamType = (TSStreamType) clipData[streamOffset + 1];
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
                        {
                            var videoFormat = (TSVideoFormat) (clipData[streamOffset + 2] >> 4);
                            var frameRate = (TSFrameRate) (clipData[streamOffset + 2] & 0xF);
                            var aspectRatio = (TSAspectRatio) (clipData[streamOffset + 3] >> 4);

                            stream = new TSVideoStream();
                            ((TSVideoStream) stream).VideoFormat = videoFormat;
                            ((TSVideoStream) stream).AspectRatio = aspectRatio;
                            ((TSVideoStream) stream).FrameRate = frameRate;
#if DEBUG
                            Debug.WriteLine(string.Format(
                                "\t{0} {1} {2} {3} {4}",
                                PID,
                                streamType,
                                videoFormat,
                                frameRate,
                                aspectRatio));
#endif
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
                            var languageBytes = new byte[3];
                            Array.Copy(clipData, streamOffset + 3, languageBytes, 0, languageBytes.Length);
                            var languageCode = Encoding.ASCII.GetString(languageBytes);

                            var channelLayout = (TSChannelLayout) (clipData[streamOffset + 2] >> 4);
                            var sampleRate = (TSSampleRate) (clipData[streamOffset + 2] & 0xF);

                            stream = new TSAudioStream();
                            ((TSAudioStream) stream).LanguageCode = languageCode;
                            ((TSAudioStream) stream).ChannelLayout = channelLayout;
                            ((TSAudioStream) stream).SampleRate = TSAudioStream.ConvertSampleRate(sampleRate);
                            ((TSAudioStream) stream).LanguageCode = languageCode;
#if DEBUG
                            Debug.WriteLine(string.Format(
                                "\t{0} {1} {2} {3} {4}",
                                PID,
                                streamType,
                                languageCode,
                                channelLayout,
                                sampleRate));
#endif
                        }
                            break;

                        case TSStreamType.INTERACTIVE_GRAPHICS:
                        case TSStreamType.PRESENTATION_GRAPHICS:
                        {
                            var languageBytes = new byte[3];
                            Array.Copy(clipData, streamOffset + 2, languageBytes, 0, languageBytes.Length);
                            var languageCode = Encoding.ASCII.GetString(languageBytes);

                            stream = new TSGraphicsStream {LanguageCode = languageCode};
#if DEBUG
                            Debug.WriteLine(string.Format(
                                "\t{0} {1} {2}",
                                PID,
                                streamType,
                                languageCode));
#endif
                        }
                            break;

                        case TSStreamType.SUBTITLE:
                        {
                            var languageBytes = new byte[3];
                            Array.Copy(clipData, streamOffset + 3, languageBytes, 0, languageBytes.Length);
                            var languageCode = Encoding.ASCII.GetString(languageBytes);
#if DEBUG
                            Debug.WriteLine(string.Format(
                                "\t{0} {1} {2}",
                                PID,
                                streamType,
                                languageCode));
#endif
                            stream = new TSTextStream();
                            stream.LanguageCode = languageCode;
                        }
                            break;
                    }

                    if (stream != null)
                    {
                        stream.PID = PID;
                        stream.StreamType = streamType;
                        Streams.Add(PID, stream);
                    }

                    streamOffset += clipData[streamOffset] + 1;
                }
            }
            finally
            {
                fileReader?.Close();
                fileStream?.Close();
                discFileStream?.Close();
            }
        }
    }
}