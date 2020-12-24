namespace BDInfo
{
    public class TSGraphicsStream : TSStream
    {
        public TSGraphicsStream()
        {
            IsVBR = true;
            IsInitialized = true;
        }

        public override TSStream Clone()
        {
            var stream = new TSGraphicsStream();
            CopyTo(stream);
            return stream;
        }
    }
}