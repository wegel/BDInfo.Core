namespace BDInfo
{
    public interface ITSVideoCodec
    {
        void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag);
    }

    public interface ITSAudioCodec
    {
        void Scan(TSAudioStream stream, TSStreamBuffer buffer, ref string tag, long? bitrate);
    }
}