namespace BDInfo
{
    public class TSTextStream : TSStream
    {
        public TSTextStream()
        {
            IsVBR = true;
            IsInitialized = true;
        }

        public override TSStream Clone()
        {
            var stream = new TSTextStream();
            CopyTo(stream);
            return stream;
        }
    }
}