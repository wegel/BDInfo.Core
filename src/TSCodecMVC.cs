namespace BDInfo
{
    // TODO: Do something more interesting here...

    public abstract class TSCodecMVC
    {
        public static void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag)
        {
            stream.IsVBR = true;
            stream.IsInitialized = true;
        }
    }
}