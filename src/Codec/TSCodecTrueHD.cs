using System;
using System.Diagnostics;

namespace BDInfo
{
    public class TSCodecTrueHD: ITSAudioCodec
    {
        public void Scan(TSAudioStream stream, TSStreamBuffer buffer, ref string tag, long? bitrate)
        {
            if (stream.IsInitialized && stream.CoreStream != null && stream.CoreStream.IsInitialized) return;

            var syncFound = false;
            uint sync = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                sync = (sync << 8) + buffer.ReadByte();
                if (sync == 0xF8726FBA)
                {
                    syncFound = true;
                    break;
                }
            }

            if (!syncFound)
            {
                tag = "CORE";
                stream.CoreStream ??= new TSAudioStream {StreamType = TSStreamType.AC3_AUDIO};
                if (stream.CoreStream.IsInitialized) return;
                buffer.BeginRead();
                (new TSCodecAC3()).Scan(stream.CoreStream, buffer, ref tag, bitrate);

                return;
            }

            tag = "HD";
            int ratebits = buffer.ReadBits2(4);
            if (ratebits != 0xF) stream.SampleRate = ((ratebits & 8) > 0 ? 44100 : 48000) << (ratebits & 7);
            buffer.BSSkipBits(15);

            stream.ChannelCount = 0;
            stream.LFE = 0;
            if (buffer.ReadBool()) stream.LFE += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.ChannelCount += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.ChannelCount += 2;
            if (buffer.ReadBool()) stream.LFE += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 1;
            if (buffer.ReadBool()) stream.ChannelCount += 2;

            buffer.BSSkipBits(49);

            var peakBitrate = buffer.ReadBits4(15);
            peakBitrate = (uint) ((peakBitrate * stream.SampleRate) >> 4);

            var peakBitdepth = (double) peakBitrate / (stream.ChannelCount + stream.LFE) / stream.SampleRate;

            stream.BitDepth = peakBitdepth > 14 ? 24 : 16;

            buffer.BSSkipBits(79);

            var hasExtensions = buffer.ReadBool();
            var numExtensions = buffer.ReadBits2(4) * 2 + 1;
            var hasContent = Convert.ToBoolean(buffer.ReadBits4(4));

            if (hasExtensions)
            {
                for (var idx = 0; idx < numExtensions; ++idx)
                    if (Convert.ToBoolean(buffer.ReadBits2(8)))
                        hasContent = true;

                if (hasContent) stream.HasExtensions = true;
            }

#if DEBUG
            Debug.WriteLine($"{stream.PID}\t{peakBitrate}\t{peakBitdepth:F2}");
#endif
            /*
            // TODO: Get THD dialnorm from metadata
            if (stream.CoreStream != null)
            {
                TSAudioStream coreStream = (TSAudioStream)stream.CoreStream;
                if (coreStream.DialNorm != 0)
                {
                    stream.DialNorm = coreStream.DialNorm;
                }
            }
            */

            stream.IsVBR = true;
            if (stream.CoreStream != null && stream.CoreStream.IsInitialized) stream.IsInitialized = true;
        }
    }
}