using System.Globalization;

namespace BDInfo
{
    public class TSVideoStream : TSStream
    {
        private TSFrameRate _frameRate;

        private TSVideoFormat _videoFormat;
        public TSAspectRatio AspectRatio;
        public string EncodingProfile;

        public object ExtendedData;
        private int _frameRateDenominator;
        private int _frameRateEnumerator;
        public int Height;
        private bool _isInterlaced;

        private int _width;

        public TSVideoFormat VideoFormat
        {
            get => _videoFormat;
            set
            {
                _videoFormat = value;
                switch (value)
                {
                    case TSVideoFormat.VIDEOFORMAT_480i:
                        Height = 480;
                        _isInterlaced = true;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_480p:
                        Height = 480;
                        _isInterlaced = false;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_576i:
                        Height = 576;
                        _isInterlaced = true;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_576p:
                        Height = 576;
                        _isInterlaced = false;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_720p:
                        Height = 720;
                        _isInterlaced = false;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_1080i:
                        Height = 1080;
                        _isInterlaced = true;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_1080p:
                        Height = 1080;
                        _isInterlaced = false;
                        break;
                    case TSVideoFormat.VIDEOFORMAT_2160p:
                        Height = 2160;
                        _isInterlaced = false;
                        break;
                }
            }
        }

        public TSFrameRate FrameRate
        {
            get => _frameRate;
            set
            {
                _frameRate = value;
                switch (value)
                {
                    case TSFrameRate.FRAMERATE_23_976:
                        _frameRateEnumerator = 24000;
                        _frameRateDenominator = 1001;
                        break;
                    case TSFrameRate.FRAMERATE_24:
                        _frameRateEnumerator = 24000;
                        _frameRateDenominator = 1000;
                        break;
                    case TSFrameRate.FRAMERATE_25:
                        _frameRateEnumerator = 25000;
                        _frameRateDenominator = 1000;
                        break;
                    case TSFrameRate.FRAMERATE_29_97:
                        _frameRateEnumerator = 30000;
                        _frameRateDenominator = 1001;
                        break;
                    case TSFrameRate.FRAMERATE_50:
                        _frameRateEnumerator = 50000;
                        _frameRateDenominator = 1000;
                        break;
                    case TSFrameRate.FRAMERATE_59_94:
                        _frameRateEnumerator = 60000;
                        _frameRateDenominator = 1001;
                        break;
                }
            }
        }

        public override string Description
        {
            get
            {
                var description = "";

                if (BaseView != null)
                {
                    if (BaseView == true)
                        description += "Right Eye";
                    else
                        description += "Left Eye";
                    description += " / ";
                }

                if (Height > 0) description += string.Format(CultureInfo.InvariantCulture, "{0:D}{1} / ", Height, _isInterlaced ? "i" : "p");
                if (_frameRateEnumerator > 0 && _frameRateDenominator > 0)
                {
                    if (_frameRateEnumerator % _frameRateDenominator == 0)
                        description += string.Format(CultureInfo.InvariantCulture, "{0:D} fps / ", _frameRateEnumerator / _frameRateDenominator);
                    else
                        description += string.Format(CultureInfo.InvariantCulture, "{0:F3} fps / ", (double) _frameRateEnumerator / _frameRateDenominator);
                }

                if (AspectRatio == TSAspectRatio.ASPECT_4_3)
                    description += "4:3 / ";
                else if (AspectRatio == TSAspectRatio.ASPECT_16_9) description += "16:9 / ";
                if (EncodingProfile != null) description += EncodingProfile + " / ";
                if (StreamType == TSStreamType.HEVC_VIDEO && ExtendedData != null)
                {
                    var extendedData = (TSCodecHEVC.ExtendedDataSet) ExtendedData;
                    var extendedInfo = string.Join(" / ", extendedData.ExtendedFormatInfo);
                    description += extendedInfo;
                }

                if (description.EndsWith(" / ")) description = description.Substring(0, description.Length - 3);
                return description;
            }
        }

        public override TSStream Clone()
        {
            var stream = new TSVideoStream();
            CopyTo(stream);

            stream.VideoFormat = _videoFormat;
            stream.FrameRate = _frameRate;
            stream._width = _width;
            stream.Height = Height;
            stream._isInterlaced = _isInterlaced;
            stream._frameRateEnumerator = _frameRateEnumerator;
            stream._frameRateDenominator = _frameRateDenominator;
            stream.AspectRatio = AspectRatio;
            stream.EncodingProfile = EncodingProfile;
            stream.ExtendedData = ExtendedData;

            return stream;
        }
    }
}