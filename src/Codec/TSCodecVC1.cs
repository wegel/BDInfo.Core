namespace BDInfo
{
    public class TSCodecVC1: ITSVideoCodec
    {
        public void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag)
        {
            var parse = 0;
            byte frameHeaderParse = 0;
            byte sequenceHeaderParse = 0;
            var isInterlaced = false;

            for (var i = 0; i < buffer.Length; i++)
            {
                parse = (parse << 8) + buffer.ReadByte();

                if (parse == 0x0000010D)
                {
                    frameHeaderParse = 4;
                }
                else if (frameHeaderParse > 0)
                {
                    --frameHeaderParse;
                    if (frameHeaderParse != 0) continue;
                    
                    uint pictureType = 0;
                    if (isInterlaced)
                    {
                        if ((parse & 0x80000000) == 0)
                            pictureType = (uint) ((parse & 0x78000000) >> 13);
                        else
                            pictureType = (uint) ((parse & 0x3c000000) >> 12);
                    }
                    else
                    {
                        pictureType = (uint) ((parse & 0xf0000000) >> 14);
                    }

                    if ((pictureType & 0x20000) == 0)
                        tag = "P";
                    else if ((pictureType & 0x10000) == 0)
                        tag = "B";
                    else if ((pictureType & 0x8000) == 0)
                        tag = "I";
                    else if ((pictureType & 0x4000) == 0)
                        tag = "BI";
                    else
                        tag = null;
                    if (stream.IsInitialized) return;
                }
                else if (parse == 0x0000010F)
                {
                    sequenceHeaderParse = 6;
                }
                else if (sequenceHeaderParse > 0)
                {
                    --sequenceHeaderParse;
                    switch (sequenceHeaderParse)
                    {
                        case 5:
                            var profileLevel = (parse & 0x38) >> 3;
                            stream.EncodingProfile = (parse & 0xC0) >> 6 == 3 ? $"Advanced Profile {profileLevel}" : $"Main Profile {profileLevel}";
                            break;

                        case 0:
                            isInterlaced = (parse & 0x40) >> 6 > 0;
                            break;
                    }

                    stream.IsVBR = true;
                    stream.IsInitialized = true;
                }
            }
        }
    }
}