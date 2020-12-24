namespace BDInfo
{
    // TODO: Do something more interesting here...

    public class TSCodecMVC: ITSVideoCodec
    {
        public void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag)
        {
            stream.IsVBR = true;
            stream.IsInitialized = true;
        }
    }
}