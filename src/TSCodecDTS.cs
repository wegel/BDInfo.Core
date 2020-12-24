namespace BDInfo
{
    public abstract class TSCodecDTS
    {
        private static readonly int[] DcaSampleRates = {0, 8000, 16000, 32000, 0, 0, 11025, 22050, 44100, 0, 0, 12000, 24000, 48000, 96000, 192000};

        private static readonly int[] DcaBitRates =
        {
            32000, 56000, 64000, 96000, 112000, 128000, 192000, 224000, 256000, 320000, 384000, 448000, 512000, 576000, 640000, 768000, 896000, 1024000,
            1152000, 1280000, 1344000, 1408000, 1411200, 1472000, 1509000, 1920000, 2048000, 3072000, 3840000, 1 /*open*/, 2 /*variable*/, 3 /*lossless*/
        };

        private static readonly int[] DcaBitsPerSample = {16, 16, 20, 20, 0, 24, 24};

        public static void Scan(TSAudioStream stream, TSStreamBuffer buffer, long bitrate, ref string tag)
        {
            if (stream.IsInitialized) return;

            var syncFound = false;
            uint sync = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                sync = (sync << 8) + buffer.ReadByte();
                if (sync == 0x7FFE8001)
                {
                    syncFound = true;
                    break;
                }
            }

            if (!syncFound) return;

            buffer.BSSkipBits(6);
            var crcPresent = buffer.ReadBits4(1);
            buffer.BSSkipBits(7);
            var frameSize = buffer.ReadBits4(14);
            if (frameSize < 95) return;
            buffer.BSSkipBits(6);
            var sampleRate = buffer.ReadBits4(4);
            if (sampleRate >= DcaSampleRates.Length) return;
            var bitRate = buffer.ReadBits4(5);
            if (bitRate >= DcaBitRates.Length) return;
            buffer.BSSkipBits(8);
            var extCoding = buffer.ReadBits4(1);
            buffer.BSSkipBits(1);
            var lfe = buffer.ReadBits4(2);
            buffer.BSSkipBits(1);
            if (crcPresent == 1) buffer.BSSkipBits(16);
            buffer.BSSkipBits(7);
            var sourcePcmRes = buffer.ReadBits4(3);
            buffer.BSSkipBits(2);
            var dialogNorm = buffer.ReadBits4(4);
            if (sourcePcmRes >= DcaBitsPerSample.Length) return;
            buffer.BSSkipBits(4);
            var totalChannels = buffer.ReadBits4(3) + 1 + extCoding;

            stream.SampleRate = DcaSampleRates[sampleRate];
            stream.ChannelCount = (int) totalChannels;
            stream.LFE = lfe > 0 ? 1 : 0;
            stream.BitDepth = DcaBitsPerSample[sourcePcmRes];
            stream.DialNorm = (int) -dialogNorm;
            if ((sourcePcmRes & 0x1) == 0x1) stream.AudioMode = TSAudioMode.Extended;

            stream.BitRate = (uint) DcaBitRates[bitRate];
            switch (stream.BitRate)
            {
                case 1:
                    if (bitrate > 0)
                    {
                        stream.BitRate = bitrate;
                        stream.IsVBR = false;
                        stream.IsInitialized = true;
                    }
                    else
                    {
                        stream.BitRate = 0;
                    }

                    break;

                case 2:
                case 3:
                    stream.IsVBR = true;
                    stream.IsInitialized = true;
                    break;

                default:
                    stream.IsVBR = false;
                    stream.IsInitialized = true;
                    break;
            }
        }
    }
}