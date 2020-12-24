using System;
using System.Globalization;

namespace BDInfo
{
    public class TSAudioStream : TSStream
    {
        public TSAudioMode AudioMode;
        public int BitDepth;
        public int ChannelCount;
        public TSChannelLayout ChannelLayout;
        public TSAudioStream CoreStream;
        public int DialNorm;

        public bool HasExtensions = false;
        public int LFE;

        public int SampleRate;

        public string ChannelDescription
        {
            get
            {
                if (ChannelLayout == TSChannelLayout.CHANNELLAYOUT_MONO && ChannelCount == 2)
                {
                }

                var description = "";
                if (ChannelCount > 0)
                    description += string.Format(CultureInfo.InvariantCulture, "{0:D}.{1:D}", ChannelCount, LFE);
                else
                    switch (ChannelLayout)
                    {
                        case TSChannelLayout.CHANNELLAYOUT_MONO:
                            description += "1.0";
                            break;
                        case TSChannelLayout.CHANNELLAYOUT_STEREO:
                            description += "2.0";
                            break;
                        case TSChannelLayout.CHANNELLAYOUT_MULTI:
                            description += "5.1";
                            break;
                    }

                if (AudioMode == TSAudioMode.Extended)
                {
                    if (StreamType == TSStreamType.AC3_AUDIO) description += "-EX";
                    if (StreamType == TSStreamType.DTS_AUDIO || StreamType == TSStreamType.DTS_HD_AUDIO || StreamType == TSStreamType.DTS_HD_MASTER_AUDIO)
                        description += "-ES";
                }

                return description;
            }
        }

        public override string Description
        {
            get
            {
                var description = ChannelDescription;

                if (SampleRate > 0) description += string.Format(CultureInfo.InvariantCulture, " / {0:D} kHz", SampleRate / 1000);
                if (BitRate > 0)
                {
                    long CoreBitRate = 0;
                    if (StreamType == TSStreamType.AC3_TRUE_HD_AUDIO && CoreStream != null) CoreBitRate = CoreStream.BitRate;
                    description += string.Format(CultureInfo.InvariantCulture, " / {0,5:D} kbps", (uint) Math.Round((double) (BitRate - CoreBitRate) / 1000));
                }

                if (BitDepth > 0) description += string.Format(CultureInfo.InvariantCulture, " / {0:D}-bit", BitDepth);
                if (DialNorm != 0) description += string.Format(CultureInfo.InvariantCulture, " / DN {0}dB", DialNorm);
                if (ChannelCount == 2)
                    switch (AudioMode)
                    {
                        case TSAudioMode.DualMono:
                            description += " / Dual Mono";
                            break;

                        case TSAudioMode.Surround:
                            description += " / Dolby Surround";
                            break;
                    }

                if (description.EndsWith(" / ")) description = description.Substring(0, description.Length - 3);
                if (CoreStream != null)
                {
                    var codec = "";
                    switch (CoreStream.StreamType)
                    {
                        case TSStreamType.AC3_AUDIO:
                            codec = "AC3 Embedded";
                            break;
                        case TSStreamType.DTS_AUDIO:
                            codec = "DTS Core";
                            break;
                        case TSStreamType.AC3_PLUS_AUDIO:
                            codec = "DD+ Embedded";
                            break;
                    }

                    description += string.Format(CultureInfo.InvariantCulture, " ({0}: {1})", codec, CoreStream.Description);
                }

                return description;
            }
        }

        public static int ConvertSampleRate(TSSampleRate sampleRate)
        {
            switch (sampleRate)
            {
                case TSSampleRate.SAMPLERATE_48:
                    return 48000;

                case TSSampleRate.SAMPLERATE_96:
                case TSSampleRate.SAMPLERATE_48_96:
                    return 96000;

                case TSSampleRate.SAMPLERATE_192:
                case TSSampleRate.SAMPLERATE_48_192:
                    return 192000;
            }

            return 0;
        }

        public override TSStream Clone()
        {
            var stream = new TSAudioStream();
            CopyTo(stream);

            stream.SampleRate = SampleRate;
            stream.ChannelLayout = ChannelLayout;
            stream.ChannelCount = ChannelCount;
            stream.BitDepth = BitDepth;
            stream.LFE = LFE;
            stream.DialNorm = DialNorm;
            stream.AudioMode = AudioMode;

            if (CoreStream != null) stream.CoreStream = (TSAudioStream) CoreStream.Clone();

            return stream;
        }
    }
}