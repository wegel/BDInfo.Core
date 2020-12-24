﻿#undef DEBUG

namespace BDInfo
{
    public class TSCodecMPEG2: ITSVideoCodec
    {
        public void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag)
        {
            var parse = 0;
            var pictureParse = 0;
            var sequenceHeaderParse = 0;
            var extensionParse = 0;
            var sequenceExtensionParse = 0;

            for (var i = 0; i < buffer.Length; i++)
            {
                parse = (parse << 8) + buffer.ReadByte();

                if (parse == 0x00000100)
                {
                    pictureParse = 2;
                }
                else if (parse == 0x000001B3)
                {
                    sequenceHeaderParse = 7;
                }
                else if (sequenceHeaderParse > 0)
                {
                    --sequenceHeaderParse;
                    switch (sequenceHeaderParse)
                    {
#if DEBUG
                        case 6:
                            break;

                        case 5:
                            break;

                        case 4:
                            stream.Width =
                                (int)((parse & 0xFFF000) >> 12);
                            stream.Height =
                                (int)(parse & 0xFFF);
                            break;

                        case 3:
                            stream.AspectRatio =
                                (TSAspectRatio)((parse & 0xF0) >> 4);

                            switch ((parse & 0xF0) >> 4)
                            {
                                case 0: // Forbidden
                                    break;
                                case 1: // Square
                                    break;
                                case 2: // 4:3
                                    break;
                                case 3: // 16:9
                                    break;
                                case 4: // 2.21:1
                                    break;
                                default: // Reserved
                                    break;
                            }

                            switch (parse & 0xF)
                            {
                                case 0: // Forbidden
                                    break;
                                case 1: // 23.976
                                    stream.FrameRateEnumerator = 24000;
                                    stream.FrameRateDenominator = 1001;
                                    break;
                                case 2: // 24
                                    stream.FrameRateEnumerator = 24000;
                                    stream.FrameRateDenominator = 1000;
                                    break;
                                case 3: // 25
                                    stream.FrameRateEnumerator = 25000;
                                    stream.FrameRateDenominator = 1000;
                                    break;
                                case 4: // 29.97
                                    stream.FrameRateEnumerator = 30000;
                                    stream.FrameRateDenominator = 1001;
                                    break;
                                case 5: // 30
                                    stream.FrameRateEnumerator = 30000;
                                    stream.FrameRateDenominator = 1000;
                                    break;
                                case 6: // 50
                                    stream.FrameRateEnumerator = 50000;
                                    stream.FrameRateDenominator = 1000;
                                    break;
                                case 7: // 59.94
                                    stream.FrameRateEnumerator = 60000;
                                    stream.FrameRateDenominator = 1001;
                                    break;
                                case 8: // 60
                                    stream.FrameRateEnumerator = 60000;
                                    stream.FrameRateDenominator = 1000;
                                    break;
                                default: // Reserved
                                    stream.FrameRateEnumerator = 0;
                                    stream.FrameRateDenominator = 0;
                                    break;
                            }
                            break;

                        case 2:
                            break;

                        case 1:
                            break;
#endif

                        case 0:
#if DEBUG
                            stream.BitRate =
                                (((parse & 0xFFFFC0) >> 6) * 200);
#endif
                            stream.IsVBR = true;
                            stream.IsInitialized = true;
                            break;
                    }
                }
                else if (pictureParse > 0)
                {
                    --pictureParse;
                    if (pictureParse == 0)
                    {
                        switch ((parse & 0x38) >> 3)
                        {
                            case 1:
                                tag = "I";
                                break;
                            case 2:
                                tag = "P";
                                break;
                            case 3:
                                tag = "B";
                                break;
                        }

                        if (stream.IsInitialized) return;
                    }
                }
                else if (parse == 0x000001B5)
                {
                    extensionParse = 1;
                }
                else if (extensionParse > 0)
                {
                    --extensionParse;
                    if (extensionParse == 0)
                        if ((parse & 0xF0) == 0x10)
                            sequenceExtensionParse = 1;
                }
                else if (sequenceExtensionParse > 0)
                {
                    --sequenceExtensionParse;
#if DEBUG
                    if (sequenceExtensionParse == 0)
                    {
                        uint sequenceExtension =
                            ((parse & 0x8) >> 3);
                        if (sequenceExtension == 0)
                        {
                            stream.IsInterlaced = true;
                        }
                        else
                        {
                            stream.IsInterlaced = false;
                        }
                    }
#endif
                }
            }
        }
    }
}