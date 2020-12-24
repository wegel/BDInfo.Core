using System.Collections.Generic;
using System.Globalization;

namespace BDInfo
{
    public enum TSStreamType : byte
    {
        Unknown = 0,
        MPEG1_VIDEO = 0x01,
        MPEG2_VIDEO = 0x02,
        AVC_VIDEO = 0x1b,
        MVC_VIDEO = 0x20,
        HEVC_VIDEO = 0x24,
        VC1_VIDEO = 0xea,
        MPEG1_AUDIO = 0x03,
        MPEG2_AUDIO = 0x04,
        LPCM_AUDIO = 0x80,
        AC3_AUDIO = 0x81,
        AC3_PLUS_AUDIO = 0x84,
        AC3_PLUS_SECONDARY_AUDIO = 0xA1,
        AC3_TRUE_HD_AUDIO = 0x83,
        DTS_AUDIO = 0x82,
        DTS_HD_AUDIO = 0x85,
        DTS_HD_SECONDARY_AUDIO = 0xA2,
        DTS_HD_MASTER_AUDIO = 0x86,
        PRESENTATION_GRAPHICS = 0x90,
        INTERACTIVE_GRAPHICS = 0x91,
        SUBTITLE = 0x92
    }

    public enum TSVideoFormat : byte
    {
        Unknown = 0,
        VIDEOFORMAT_480i = 1,
        VIDEOFORMAT_576i = 2,
        VIDEOFORMAT_480p = 3,
        VIDEOFORMAT_1080i = 4,
        VIDEOFORMAT_720p = 5,
        VIDEOFORMAT_1080p = 6,
        VIDEOFORMAT_576p = 7,
        VIDEOFORMAT_2160p = 8
    }

    public enum TSFrameRate : byte
    {
        Unknown = 0,
        FRAMERATE_23_976 = 1,
        FRAMERATE_24 = 2,
        FRAMERATE_25 = 3,
        FRAMERATE_29_97 = 4,
        FRAMERATE_50 = 6,
        FRAMERATE_59_94 = 7
    }

    public enum TSChannelLayout : byte
    {
        Unknown = 0,
        CHANNELLAYOUT_MONO = 1,
        CHANNELLAYOUT_STEREO = 3,
        CHANNELLAYOUT_MULTI = 6,
        CHANNELLAYOUT_COMBO = 12
    }

    public enum TSSampleRate : byte
    {
        Unknown = 0,
        SAMPLERATE_48 = 1,
        SAMPLERATE_96 = 4,
        SAMPLERATE_192 = 5,
        SAMPLERATE_48_192 = 12,
        SAMPLERATE_48_96 = 14
    }

    public enum TSAspectRatio
    {
        Unknown = 0,
        ASPECT_4_3 = 2,
        ASPECT_16_9 = 3,
        ASPECT_2_21 = 4
    }

    public class TSDescriptor
    {
        public readonly byte Name;
        public readonly byte[] Value;

        public TSDescriptor(byte name, byte length)
        {
            Name = name;
            Value = new byte[length];
        }

        public TSDescriptor Clone()
        {
            var descriptor = new TSDescriptor(Name, (byte) Value.Length);
            Value.CopyTo(descriptor.Value, 0);
            return descriptor;
        }
    }

    public abstract class TSStream
    {
        private string _languageCode;
        public long ActiveBitRate = 0;
        public int AngleIndex = 0;

        public bool? BaseView;
        public long BitRate;
        public List<TSDescriptor> Descriptors;
        public bool IsHidden = false;
        public bool IsInitialized;
        public bool IsVBR;
        public string LanguageName;
        public ulong PacketCount = 0;
        public double PacketSeconds = 0;

        public ulong PayloadBytes = 0;

        public ushort PID;
        public TSStreamType StreamType;

        public ulong PacketSize => PacketCount * 192;

        public string LanguageCode
        {
            get => _languageCode;
            set
            {
                _languageCode = value;
                LanguageName = LanguageCodes.GetName(value);
            }
        }

        public bool IsVideoStream
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.MPEG1_VIDEO:
                    case TSStreamType.MPEG2_VIDEO:
                    case TSStreamType.AVC_VIDEO:
                    case TSStreamType.MVC_VIDEO:
                    case TSStreamType.VC1_VIDEO:
                    case TSStreamType.HEVC_VIDEO:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public bool IsAudioStream
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.MPEG1_AUDIO:
                    case TSStreamType.MPEG2_AUDIO:
                    case TSStreamType.LPCM_AUDIO:
                    case TSStreamType.AC3_AUDIO:
                    case TSStreamType.AC3_PLUS_AUDIO:
                    case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                    case TSStreamType.AC3_TRUE_HD_AUDIO:
                    case TSStreamType.DTS_AUDIO:
                    case TSStreamType.DTS_HD_AUDIO:
                    case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                    case TSStreamType.DTS_HD_MASTER_AUDIO:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public bool IsGraphicsStream
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.PRESENTATION_GRAPHICS:
                    case TSStreamType.INTERACTIVE_GRAPHICS:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public bool IsTextStream
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.SUBTITLE:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public string CodecName
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.MPEG1_VIDEO:
                        return "MPEG-1 Video";
                    case TSStreamType.MPEG2_VIDEO:
                        return "MPEG-2 Video";
                    case TSStreamType.AVC_VIDEO:
                        return "MPEG-4 AVC Video";
                    case TSStreamType.MVC_VIDEO:
                        return "MPEG-4 MVC Video";
                    case TSStreamType.HEVC_VIDEO:
                        return "MPEG-H HEVC Video";
                    case TSStreamType.VC1_VIDEO:
                        return "VC-1 Video";
                    case TSStreamType.MPEG1_AUDIO:
                        return "MP1 Audio";
                    case TSStreamType.MPEG2_AUDIO:
                        return "MP2 Audio";
                    case TSStreamType.LPCM_AUDIO:
                        return "LPCM Audio";
                    case TSStreamType.AC3_AUDIO:
                        if (((TSAudioStream) this).AudioMode == TSAudioMode.Extended)
                            return "Dolby Digital EX Audio";
                        else
                            return "Dolby Digital Audio";
                    case TSStreamType.AC3_PLUS_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "Dolby Digital Plus/Atmos Audio";
                        else
                            return "Dolby Digital Plus Audio";
                    case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                        return "Dolby Digital Plus Audio";
                    case TSStreamType.AC3_TRUE_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "Dolby TrueHD/Atmos Audio";
                        else
                            return "Dolby TrueHD Audio";
                    case TSStreamType.DTS_AUDIO:
                        if (((TSAudioStream) this).AudioMode == TSAudioMode.Extended)
                            return "DTS-ES Audio";
                        else
                            return "DTS Audio";
                    case TSStreamType.DTS_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X High-Res Audio";
                        else
                            return "DTS-HD High-Res Audio";
                    case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                        return "DTS Express";
                    case TSStreamType.DTS_HD_MASTER_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X Master Audio";
                        else
                            return "DTS-HD Master Audio";
                    case TSStreamType.PRESENTATION_GRAPHICS:
                        return "Presentation Graphics";
                    case TSStreamType.INTERACTIVE_GRAPHICS:
                        return "Interactive Graphics";
                    case TSStreamType.SUBTITLE:
                        return "Subtitle";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        public string CodecAltName
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.MPEG1_VIDEO:
                        return "MPEG-1";
                    case TSStreamType.MPEG2_VIDEO:
                        return "MPEG-2";
                    case TSStreamType.AVC_VIDEO:
                        return "AVC";
                    case TSStreamType.MVC_VIDEO:
                        return "MVC";
                    case TSStreamType.HEVC_VIDEO:
                        return "HEVC";
                    case TSStreamType.VC1_VIDEO:
                        return "VC-1";
                    case TSStreamType.MPEG1_AUDIO:
                        return "MP1";
                    case TSStreamType.MPEG2_AUDIO:
                        return "MP2";
                    case TSStreamType.LPCM_AUDIO:
                        return "LPCM";
                    case TSStreamType.AC3_AUDIO:
                        return "DD AC3";
                    case TSStreamType.AC3_PLUS_AUDIO:
                    case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                        return "DD AC3+";
                    case TSStreamType.AC3_TRUE_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "Dolby Atmos";
                        else
                            return "Dolby TrueHD";
                    case TSStreamType.DTS_AUDIO:
                        return "DTS";
                    case TSStreamType.DTS_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X Hi-Res";
                        else
                            return "DTS-HD Hi-Res";
                    case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                        return "DTS Express";
                    case TSStreamType.DTS_HD_MASTER_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X Master";
                        else
                            return "DTS-HD Master";
                    case TSStreamType.PRESENTATION_GRAPHICS:
                        return "PGS";
                    case TSStreamType.INTERACTIVE_GRAPHICS:
                        return "IGS";
                    case TSStreamType.SUBTITLE:
                        return "SUB";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        public string CodecShortName
        {
            get
            {
                switch (StreamType)
                {
                    case TSStreamType.MPEG1_VIDEO:
                        return "MPEG-1";
                    case TSStreamType.MPEG2_VIDEO:
                        return "MPEG-2";
                    case TSStreamType.AVC_VIDEO:
                        return "AVC";
                    case TSStreamType.MVC_VIDEO:
                        return "MVC";
                    case TSStreamType.HEVC_VIDEO:
                        return "HEVC";
                    case TSStreamType.VC1_VIDEO:
                        return "VC-1";
                    case TSStreamType.MPEG1_AUDIO:
                        return "MP1";
                    case TSStreamType.MPEG2_AUDIO:
                        return "MP2";
                    case TSStreamType.LPCM_AUDIO:
                        return "LPCM";
                    case TSStreamType.AC3_AUDIO:
                        if (((TSAudioStream) this).AudioMode == TSAudioMode.Extended)
                            return "AC3-EX";
                        else
                            return "AC3";
                    case TSStreamType.AC3_PLUS_AUDIO:
                    case TSStreamType.AC3_PLUS_SECONDARY_AUDIO:
                        return "AC3+";
                    case TSStreamType.AC3_TRUE_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "Atmos";
                        else
                            return "TrueHD";
                    case TSStreamType.DTS_AUDIO:
                        if (((TSAudioStream) this).AudioMode == TSAudioMode.Extended)
                            return "DTS-ES";
                        else
                            return "DTS";
                    case TSStreamType.DTS_HD_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X HR";
                        else
                            return "DTS-HD HR";
                    case TSStreamType.DTS_HD_SECONDARY_AUDIO:
                        return "DTS Express";
                    case TSStreamType.DTS_HD_MASTER_AUDIO:
                        if (((TSAudioStream) this).HasExtensions)
                            return "DTS:X MA";
                        else
                            return "DTS-HD MA";
                    case TSStreamType.PRESENTATION_GRAPHICS:
                        return "PGS";
                    case TSStreamType.INTERACTIVE_GRAPHICS:
                        return "IGS";
                    case TSStreamType.SUBTITLE:
                        return "SUB";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        public virtual string Description => "";

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1})", CodecShortName, PID);
        }

        public abstract TSStream Clone();

        protected void CopyTo(TSStream stream)
        {
            stream.PID = PID;
            stream.StreamType = StreamType;
            stream.IsVBR = IsVBR;
            stream.BitRate = BitRate;
            stream.IsInitialized = IsInitialized;
            stream.LanguageCode = _languageCode;

            if (Descriptors != null)
            {
                stream.Descriptors = new List<TSDescriptor>();
                foreach (var descriptor in Descriptors) stream.Descriptors.Add(descriptor.Clone());
            }
        }
    }

    public enum TSAudioMode
    {
        Unknown,
        DualMono,
        Stereo,
        Surround,
        Extended
    }
}