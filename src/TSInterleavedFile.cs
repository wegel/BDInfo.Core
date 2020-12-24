using System.IO;
using DiscUtils;
using DiscUtils.Udf;

// TODO: Do more interesting things here...

namespace BDInfo
{
    public class TSInterleavedFile
    {
        private UdfReader _cdReader;
        public readonly DiscFileInfo DFileInfo;

        public readonly FileInfo FileInfo;
        public readonly string Name;

        public TSInterleavedFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            DFileInfo = null;
            _cdReader = null;
            Name = fileInfo.Name.ToUpper();
        }

        public TSInterleavedFile(DiscFileInfo fileInfo, UdfReader reader)
        {
            DFileInfo = fileInfo;
            FileInfo = null;
            _cdReader = reader;
            Name = fileInfo.Name.ToUpper();
        }
    }
}