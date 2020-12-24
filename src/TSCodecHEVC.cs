using System;
using System.Collections.Generic;
using System.Globalization;

namespace BDInfo
{
    public abstract class TSCodecHEVC
    {
        private static uint _chromaSampleLocTypeTopField;
        private static uint _chromaSampleLocTypeBottomField;

        private static uint _profileSpace;
        private static bool _tierFlag;
        private static ushort _profileIDC;
        private static ushort _levelIDC;
        private static bool _generalProgressiveSourceFlag;
        private static bool _generalInterlacedSourceFlag;
        private static bool _generalFrameOnlyConstraintFlag;

        private static ExtendedDataSet _extendedData;

        private static List<VideoParamSetStruct> _videoParamSets;
        private static List<VUIParametersStruct> _vuiParameterSets;
        private static List<SeqParameterSetStruct> _seqParameterSets;
        private static List<PicParameterSetStruct> _picParameterSets;

        private static string _masteringDisplayColorPrimaries;
        private static string _masteringDisplayLuminance;
        private static uint _maximumContentLightLevel;
        private static uint _maximumFrameAverageLightLevel;

        private static List<string> _extendedFormatInfo;
        private static bool _lightLevelAvailable;

        private static byte _preferredTransferCharacteristics;

        private static string ColourPrimaries(byte colourPrimaries)
        {
            switch (colourPrimaries)
            {
                case 1:
                    return "BT.709";
                case 4:
                    return "BT.470 System M";
                case 5:
                    return "BT.601 PAL";
                case 6:
                    return "BT.601 NTSC";
                case 7:
                    return "SMPTE 240M"; //Same as BT.601 NTSC
                case 8:
                    return "Generic film";
                case 9:
                    return "BT.2020"; //Added in HEVC
                case 10:
                    return "XYZ"; //Added in HEVC 2014
                case 11:
                    return "DCI P3"; //Added in HEVC 2016
                case 12:
                    return "Display P3"; //Added in HEVC 2016
                case 22:
                    return "EBU Tech 3213"; //Added in HEVC 2016
                default:
                    return "";
            }
        }

        private static string TransferCharacteristics(byte transferCharacteristics)
        {
            switch (transferCharacteristics)
            {
                case 1:
                    return "BT.709"; //Same as BT.601
                case 4:
                    return "BT.470 System M";
                case 5:
                    return "BT.470 System B/G";
                case 6:
                    return "BT.601";
                case 7:
                    return "SMPTE 240M";
                case 8:
                    return "Linear";
                case 9:
                    return "Logarithmic (100:1)"; //Added in MPEG-4 Visual
                case 10:
                    return "Logarithmic (316.22777:1)"; //Added in MPEG-4 Visual
                case 11:
                    return "xvYCC"; //Added in AVC
                case 12:
                    return "BT.1361"; //Added in AVC
                case 13:
                    return "sRGB/sYCC"; //Added in HEVC
                case 14:
                    return "BT.2020 (10-bit)"; //Same a BT.601            //Added in HEVC, 10/12-bit difference is in ISO 23001-8
                case 15:
                    return "BT.2020 (12-bit)"; //Same a BT.601            //Added in HEVC, 10/12-bit difference is in ISO 23001-8
                case 16:
                    return "PQ"; //Added in HEVC 2015
                case 17:
                    return "SMPTE 428M"; //Added in HEVC 2015
                case 18:
                    return "HLG"; //Added in HEVC 2016
                default:
                    return "";
            }
        }

        private static string MatrixCoefficients(byte matrixCoefficients)
        {
            switch (matrixCoefficients)
            {
                case 0:
                    return "Identity"; //Added in AVC
                case 1:
                    return "BT.709";
                case 4:
                    return "FCC 73.682";
                case 5:
                    return "BT.470 System B/G";
                case 6:
                    return "BT.601"; //Same as BT.470 System B/G
                case 7:
                    return "SMPTE 240M";
                case 8:
                    return "YCgCo"; //Added in AVC
                case 9:
                    return "BT.2020 non-constant"; //Added in HEVC
                case 10:
                    return "BT.2020 constant"; //Added in HEVC
                case 11:
                    return "Y'D'zD'x"; //Added in HEVC 2016
                case 12:
                    return "Chromaticity-derived non-constant"; //Added in HEVC 2016
                case 13:
                    return "Chromaticity-derived constant"; //Added in HEVC 2016
                case 14:
                    return "ICtCp"; //Added in HEVC 2016
                default:
                    return "";
            }
        }

        public static void Scan(TSVideoStream stream, TSStreamBuffer buffer, ref string tag)
        {
            if (stream.IsInitialized) return;

            if (stream.ExtendedData == null) stream.ExtendedData = new ExtendedDataSet();

            _extendedData = (ExtendedDataSet) stream.ExtendedData;

            _videoParamSets = _extendedData.VideoParamSets;
            _vuiParameterSets = _extendedData.VUIParameterSets;
            _seqParameterSets = _extendedData.SeqParameterSets;
            _picParameterSets = _extendedData.PicParameterSets;

            _masteringDisplayColorPrimaries = _extendedData.MasteringDisplayColorPrimaries;
            _masteringDisplayLuminance = _extendedData.MasteringDisplayLuminance;

            _maximumContentLightLevel = _extendedData.MaximumContentLightLevel;
            _maximumFrameAverageLightLevel = _extendedData.MaximumFrameAverageLightLevel;

            _lightLevelAvailable = _extendedData.LightLevelAvailable;

            _preferredTransferCharacteristics = _extendedData.PreferredTransferCharacteristics;

            _extendedFormatInfo = _extendedData.ExtendedFormatInfo;

            do
            {
                do
                {
                    var streamPos = buffer.Position;
                    if (buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x1) break;

                    buffer.BSSkipBytes((int) (streamPos - buffer.Position));
                    if (buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x1) break;
                    buffer.BSSkipBytes((int) (streamPos - buffer.Position + 1));
                } while (buffer.Position < buffer.Length);

                if (buffer.Position < buffer.Length)
                {
                    var lastStreamPos = buffer.Position;

                    buffer.BSSkipBits(1); // skip 1 bit

                    long nalUnitType = buffer.ReadBits2(6);

                    buffer.BSSkipBits(9); // nuh_layer_id (6), nuh_temporal_id_plus1 (3)

                    switch (nalUnitType)
                    {
                        case 32:
                            VideoParameterSet(buffer);
                            break;
                        case 33:
                            SeqParameterSet(buffer);
                            break;
                        case 34:
                            PicParameterSet(buffer);
                            break;
                        case 35:
                            AccessUnitDelimiter(buffer);
                            break;
                        case 39:
                        case 40:
                            Sei(buffer);
                            break;
                    }

                    buffer.BSSkipNextByte();
                    buffer.BSSkipBytes((int) (lastStreamPos - buffer.Position));
                }
            } while (buffer.Position < buffer.Length);

            _extendedData.PreferredTransferCharacteristics = _preferredTransferCharacteristics;

            _extendedData.LightLevelAvailable = _lightLevelAvailable;

            _extendedData.MaximumContentLightLevel = _maximumContentLightLevel;
            _extendedData.MaximumFrameAverageLightLevel = _maximumFrameAverageLightLevel;

            _extendedData.MasteringDisplayColorPrimaries = _masteringDisplayColorPrimaries;
            _extendedData.MasteringDisplayLuminance = _masteringDisplayLuminance;

            _extendedData.VideoParamSets = _videoParamSets;
            _extendedData.VUIParameterSets = _vuiParameterSets;
            _extendedData.SeqParameterSets = _seqParameterSets;
            _extendedData.PicParameterSets = _picParameterSets;

            stream.ExtendedData = _extendedData;

            // TODO: profile to string
            if (_seqParameterSets.Count > 0)
            {
                var seqParameterSet = _seqParameterSets[0];
                if (seqParameterSet.ProfileSpace == 0)
                {
                    var profile = string.Empty;
                    if (seqParameterSet.ProfileIDC > 0)
                        switch (seqParameterSet.ProfileIDC)
                        {
                            case 0:
                                profile = "No profile";
                                break;
                            case 1:
                                profile = "Main";
                                break;
                            case 2:
                                profile = "Main 10";
                                break;
                            case 3:
                                profile = "Main Still";
                                break;
                            default:
                                profile = "";
                                break;
                        }

                    if (seqParameterSet.LevelIDC > 0)
                    {
                        var calcLevel = (float) _levelIDC / 30;
                        var dec = _levelIDC % 10;
                        profile += " @ Level " + string.Format(CultureInfo.InvariantCulture, dec >= 1 ? "{0:0.0}" : "{0:0}", calcLevel) + " @ ";
                        if (_tierFlag)
                            profile += "High";
                        else
                            profile += "Main";
                    }

                    stream.EncodingProfile = profile;

                    if (seqParameterSet.ChromaFormatIDC > 0)
                    {
                        var chromaFormat = string.Empty;
                        switch (seqParameterSet.ChromaFormatIDC)
                        {
                            case 1:
                                chromaFormat = "4:2:0";
                                break;
                            case 2:
                                chromaFormat = "4:2:2";
                                break;
                            case 3:
                                chromaFormat = "4:4:4";
                                break;
                        }

                        if (chromaFormat != string.Empty && BDInfoSettings.ExtendedStreamDiagnostics) _extendedFormatInfo.Add(chromaFormat);
                    }

                    if (seqParameterSet.BitDepthLumaMinus8 == seqParameterSet.BitDepthChromaMinus8)
                        _extendedFormatInfo.Add($"{seqParameterSet.BitDepthLumaMinus8 + 8} bits");

                    if (seqParameterSet.BitDepthLumaMinus8 + 8 == 10 && // 10 bit
                        seqParameterSet.ChromaFormatIDC == 1 && // ChromaFormat 4:2:0
                        seqParameterSet.VUIParameters.VideoSignalTypePresentFlag && seqParameterSet.VUIParameters.ColourDescriptionPresentFlag &&
                        seqParameterSet.VUIParameters.ColourPrimaries == 9 && //ColourPrimaries BT.2020
                        seqParameterSet.VUIParameters.TransferCharacteristics == 16 && //TransferCharacteristics PQ
                        (seqParameterSet.VUIParameters.MatrixCoefficients == 9 || //MatrixCoefficients BT.2020 non-constant
                         seqParameterSet.VUIParameters.MatrixCoefficients == 10) && //MatrixCoefficients BT.2020 constant
                        !string.IsNullOrEmpty(_masteringDisplayColorPrimaries))
                        _extendedFormatInfo.Add(stream.PID >= 4117 ? "Dolby Vision" : "HDR10");

                    if (seqParameterSet.VUIParameters.VideoSignalTypePresentFlag)
                    {
                        if (BDInfoSettings.ExtendedStreamDiagnostics)
                            _extendedFormatInfo.Add(seqParameterSet.VUIParameters.VideoFullRangeFlag == 1 ? "Full Range" : "Limited Range");

                        if (seqParameterSet.VUIParameters.ColourDescriptionPresentFlag)
                        {
                            _extendedFormatInfo.Add(ColourPrimaries(seqParameterSet.VUIParameters.ColourPrimaries));
                            if (BDInfoSettings.ExtendedStreamDiagnostics)
                            {
                                _extendedFormatInfo.Add(TransferCharacteristics(seqParameterSet.VUIParameters.TransferCharacteristics));
                                _extendedFormatInfo.Add(MatrixCoefficients(seqParameterSet.VUIParameters.MatrixCoefficients));
                            }
                        }
                    }
                }
            }

            if (BDInfoSettings.ExtendedStreamDiagnostics)
            {
                if (_masteringDisplayColorPrimaries != string.Empty)
                    _extendedFormatInfo.Add("Mastering display color primaries: " + _masteringDisplayColorPrimaries);
                if (_masteringDisplayLuminance != string.Empty) _extendedFormatInfo.Add("Mastering display luminance: " + _masteringDisplayLuminance);

                if (_lightLevelAvailable && _maximumContentLightLevel > 0)
                {
                    _extendedFormatInfo.Add("Maximum Content Light Level: " + _maximumContentLightLevel + " cd / m2");
                    _extendedFormatInfo.Add("Maximum Frame-Average Light Level: " + _maximumFrameAverageLightLevel + " cd/m2");
                }
            }

            stream.IsVBR = true;
            if (_seqParameterSets.Count > 0) stream.IsInitialized = true;
        }

        // packet 32
        private static void VideoParameterSet(TSStreamBuffer buffer)
        {
            int vpsVideoParameterSetID = buffer.ReadBits2(4);

            buffer.BSSkipBits(8); //vps_reserved_three_2bits, vps_reserved_zero_6bits

            var maxSubLayers = buffer.ReadBits2(3); //vps_max_sub_layers_minus1

            buffer.BSSkipBits(17); //vps_temporal_id_nesting_flag, vps_reserved_0xffff_16bits

            ProfileTierLevel(buffer, maxSubLayers);

            var tempB = buffer.ReadBool(); //vps_sub_layer_ordering_info_present_flag
            for (var subLayerPos = tempB ? 0 : maxSubLayers; subLayerPos <= maxSubLayers; subLayerPos++)
                buffer.SkipExpMulti(3); //vps_max_dec_pic_buffering_minus1, vps_max_num_reorder_pics, vps_max_latency_increase_plus1

            var vpsMaxLayerID = buffer.ReadBits2(6);
            var vpsNumLayerSetsMinus1 = buffer.ReadExp(); //vps_num_layer_sets_minus1

            for (var layerSetPos = 1; layerSetPos <= vpsNumLayerSetsMinus1; layerSetPos++)
            for (var layerId = 0; layerId <= vpsMaxLayerID; layerId++)
                buffer.BSSkipBits(1); //layer_id_included_flag

            var vpsTimingInfoPresentFlag = buffer.ReadBool();
            if (vpsTimingInfoPresentFlag)
            {
                buffer.BSSkipBits(64); //vps_num_units_in_tick, vps_time_scale

                var vpsPocProportionalToTimingFlag = buffer.ReadBool();
                if (!vpsPocProportionalToTimingFlag) buffer.SkipExp(); //vps_num_ticks_poc_diff_one_minus1

                var vpsNumHRDParameters = (int) buffer.ReadExp();
                if (vpsNumHRDParameters > 1024) vpsNumHRDParameters = 0;
                for (var hrdPos = 0; hrdPos < vpsNumHRDParameters; hrdPos++)
                {
                    XXLCommon xxlCommon = null;
                    XXL nal = null, vcl = null;

                    buffer.SkipExp(); //hrd_layer_sed_idx
                    var cprmsPresentFlag = hrdPos <= 0 || buffer.ReadBool();
                    HRDParameters(buffer, cprmsPresentFlag, vpsNumLayerSetsMinus1, ref xxlCommon, ref nal, ref vcl);
                }
            }

            buffer.BSSkipBits(1); //vps_extension_flag

            if (vpsVideoParameterSetID >= _videoParamSets.Count)
                for (var i = _videoParamSets.Count - 1; i < vpsVideoParameterSetID; i++)
                    _videoParamSets.Add(new VideoParamSetStruct(0));
            _videoParamSets[vpsVideoParameterSetID] = new VideoParamSetStruct((ushort) vpsNumLayerSetsMinus1);
        }

        // packet 33
        private static void SeqParameterSet(TSStreamBuffer buffer)
        {
            var vuiParametersItem = new VUIParametersStruct();
            var videoParamSetItem = new VideoParamSetStruct();

            uint confWinLeftOffset = 0, confWinRightOffset = 0, confWinTopOffset = 0, confWinBottomOffset = 0;
            var separateColourPlaneFlag = false;

            uint videoParameterSetID = buffer.ReadBits2(4);

            if (videoParameterSetID >= _videoParamSets.Count || _videoParamSets[(int) videoParameterSetID] == null) return;

            uint maxSubLayersMinus1 = buffer.ReadBits2(3);
            buffer.BSSkipBits(1);
            ProfileTierLevel(buffer, maxSubLayersMinus1);
            var spsSeqParameterSetID = buffer.ReadExp();
            var chromaFormatIDC = buffer.ReadExp();
            if (chromaFormatIDC >= 4) return;
            if (chromaFormatIDC == 3) separateColourPlaneFlag = buffer.ReadBool();
            var picWidthInLumaSamples = buffer.ReadExp();
            var picHeightInLumaSamples = buffer.ReadExp();
            if (buffer.ReadBool()) //conformance_window_flag
            {
                confWinLeftOffset = buffer.ReadExp();
                confWinRightOffset = buffer.ReadExp();
                confWinTopOffset = buffer.ReadExp();
                confWinBottomOffset = buffer.ReadExp();
            }

            var bitDepthLumaMinus8 = buffer.ReadExp();
            if (bitDepthLumaMinus8 > 6) return;
            var bitDepthChromaMinus8 = buffer.ReadExp();
            if (bitDepthChromaMinus8 > 6) return;
            var log2MaxPicOrderCntLsbMinus4 = buffer.ReadExp();
            if (log2MaxPicOrderCntLsbMinus4 > 12) return;
            var spsSubLayerOrderingInfoPresentFlag = buffer.ReadBool();
            for (var subLayerPos = spsSubLayerOrderingInfoPresentFlag ? 0 : maxSubLayersMinus1; subLayerPos <= maxSubLayersMinus1; subLayerPos++)
                buffer.SkipExpMulti(3);
            buffer.SkipExpMulti(6);

            if (buffer.ReadBool()) //scaling_list_enabled_flag
                if (buffer.ReadBool()) //sps_scaling_list_data_present_flag
                    ScalingListData(buffer);

            buffer.BSSkipBits(2);
            if (buffer.ReadBool()) //pcm_enabled_flag
            {
                buffer.BSSkipBits(8);
                buffer.SkipExpMulti(2);
                buffer.BSSkipBits(1);
            }

            var numShortTermRefPicSets = buffer.ReadExp();
            ShortTermRefPicSets(buffer, numShortTermRefPicSets);
            if (buffer.ReadBool()) //long_term_ref_pics_present_flag
            {
                var numLongTermRefPicsSps = buffer.ReadExp();
                for (var i = 0; i < numLongTermRefPicsSps; i++)
                {
                    buffer.BSSkipBits((int) (log2MaxPicOrderCntLsbMinus4 + 4));
                    buffer.BSSkipBits(1);
                }
            }

            buffer.BSSkipBits(2);
            if (buffer.ReadBool()) //vui_parameters_present_flag
                VUIParameters(buffer, videoParamSetItem, ref vuiParametersItem);

            if (spsSeqParameterSetID >= _seqParameterSets.Count)
                for (var i = _seqParameterSets.Count - 1; i < spsSeqParameterSetID; i++)
                    _seqParameterSets.Add(new SeqParameterSetStruct());
            _seqParameterSets[(int) spsSeqParameterSetID] = new SeqParameterSetStruct(vuiParametersItem, _profileSpace, _tierFlag, _profileIDC, _levelIDC,
                picWidthInLumaSamples, picHeightInLumaSamples, confWinLeftOffset, confWinRightOffset, confWinTopOffset, confWinBottomOffset,
                (byte) videoParameterSetID, (byte) chromaFormatIDC, separateColourPlaneFlag, (byte) log2MaxPicOrderCntLsbMinus4, (byte) bitDepthLumaMinus8,
                (byte) bitDepthChromaMinus8, _generalProgressiveSourceFlag, _generalInterlacedSourceFlag, _generalFrameOnlyConstraintFlag);
        }

        // packet 34
        private static void PicParameterSet(TSStreamBuffer buffer)
        {
            var ppsPicParameterSetID = buffer.ReadExp();
            if (ppsPicParameterSetID >= 64) return;
            var ppsSeqParameterSetID = buffer.ReadExp();
            if (ppsSeqParameterSetID >= 16) return;
            if (ppsSeqParameterSetID >= _seqParameterSets.Count || _seqParameterSets[(int) ppsSeqParameterSetID] == null) return;

            var dependentSliceSegmentsEnabledFlag = buffer.ReadBool();
            buffer.BSSkipBits(1);
            var numExtraSliceHeaderBits = (byte) buffer.ReadBits2(3);
            buffer.BSSkipBits(2);
            var numRefIdxL0DefaultActiveMinus1 = buffer.ReadExp();
            var numRefIdxL1DefaultActiveMinus1 = buffer.ReadExp();
            buffer.SkipExp();
            buffer.BSSkipBits(2);
            if (buffer.ReadBool()) //cu_qp_delta_enabled_flag
                buffer.SkipExp(); //diff_cu_qp_delta_depth
            buffer.SkipExpMulti(2);
            buffer.BSSkipBits(4);
            var tilesEnabledFlag = buffer.ReadBool();
            buffer.BSSkipBits(1);
            if (tilesEnabledFlag)
            {
                var numTileColumnsMinus1 = buffer.ReadExp();
                var numTileRowsMinus1 = buffer.ReadExp();
                var uniformSpacingFlag = buffer.ReadBool();
                if (!uniformSpacingFlag)
                {
                    buffer.SkipExpMulti((int) numTileColumnsMinus1);
                    buffer.SkipExpMulti((int) numTileRowsMinus1);
                }

                buffer.BSSkipBits(1);
            }

            buffer.BSSkipBits(1);
            if (buffer.ReadBool()) //deblocking_filter_control_present_flag
            {
                buffer.BSSkipBits(1);
                if (!buffer.ReadBool()) //pps_disable_deblocking_filter_flag
                    buffer.SkipExpMulti(2);
            }

            if (buffer.ReadBool()) //pps_scaling_list_data_present_flag
                ScalingListData(buffer);
            buffer.BSSkipBits(1);
            buffer.SkipExp();
            buffer.BSSkipBits(1);
            if (buffer.ReadBool()) //pps_extension_flag
                buffer.BSSkipNextByte();

            if (ppsPicParameterSetID >= _picParameterSets.Count)
                for (var i = _picParameterSets.Count - 1; i < ppsPicParameterSetID; i++)
                    _picParameterSets.Add(new PicParameterSetStruct());
            _picParameterSets[(int) ppsPicParameterSetID] = new PicParameterSetStruct((byte) ppsSeqParameterSetID, (byte) numRefIdxL0DefaultActiveMinus1,
                (byte) numRefIdxL1DefaultActiveMinus1, numExtraSliceHeaderBits, dependentSliceSegmentsEnabledFlag);
        }

        // packet 35
        private static void AccessUnitDelimiter(TSStreamBuffer buffer)
        {
            buffer.BSSkipBits(3);
        }

        // packet 39 & 40
        private static void Sei(TSStreamBuffer buffer)
        {
            var elementStart = buffer.Position;

            int numBytes;

            // find element end
            do
            {
                var streamPos = buffer.Position;
                numBytes = 0;
                if (buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x1)
                {
                    numBytes = 4;
                    break;
                }

                buffer.BSSkipBytes((int) (streamPos - buffer.Position));
                if (buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x0 && buffer.ReadByte() == 0x1)
                {
                    numBytes = 3;
                    break;
                }

                buffer.BSSkipBytes((int) (streamPos - buffer.Position + 1));
            } while (buffer.Position < buffer.Length);

            var elementSize = buffer.Position - elementStart;

            buffer.BSSkipBytes((int) (elementSize * -1));

            elementSize -= numBytes + 1;

            do
            {
                var seqParameterSetID = uint.MaxValue;
                uint payloadType = 0, payloadSize = 0;
                byte payloadTypeByte, payloadSizeByte;

                do
                {
                    payloadTypeByte = buffer.ReadByte();
                    payloadType += payloadTypeByte;
                } while (payloadTypeByte == 0xFF);

                do
                {
                    payloadSizeByte = buffer.ReadByte();
                    payloadSize += payloadSizeByte;
                } while (payloadSizeByte == 0xFF);

                var SavedPos = (ulong) buffer.Position + payloadSize;

                if (SavedPos > (ulong) buffer.Length) // wrong length
                    return;

                switch (payloadType)
                {
                    case 0:
                        SeiMessageBufferingPeriod(buffer, ref seqParameterSetID, payloadSize);
                        break;
                    case 1:
                        SeiMessagePicTiming(buffer, ref seqParameterSetID, payloadSize);
                        break;
                    case 6:
                        SeiMessageRecoveryPoint(buffer);
                        break;
                    case 129:
                        SeiMessageActiveParametersSets(buffer);
                        break;
                    case 137:
                        SeiMessageMasteringDisplayColourVolume(buffer);
                        break;
                    case 144:
                        SeiMessageLightLevel(buffer);
                        break;
                    case 147:
                        SeiAlternativeTransferCharacteristics(buffer);
                        break;
                    default:
                        buffer.BSSkipBytes((int) payloadSize);
                        break;
                }

                if (SavedPos > (ulong) buffer.Position) buffer.BSSkipBytes((int) (SavedPos - (ulong) buffer.Position));
            } while (buffer.Position < elementStart + elementSize);
        }

        // SEI - 0
        private static void SeiMessageBufferingPeriod(TSStreamBuffer buffer, ref uint seqParameterSetID, uint payloadSize)
        {
            seqParameterSetID = buffer.ReadExp();
            SeqParameterSetStruct seqParameterSetItem;
            if (seqParameterSetID >= _seqParameterSets.Count || (seqParameterSetItem = _seqParameterSets[(int) seqParameterSetID]) == null)
            {
                buffer.BSSkipBits((int) (payloadSize * 8));
                return;
            }

            var subPicHRDParamsPresentFlag = false; //Default
            var irapCPBParamsPresentFlag = seqParameterSetItem.VUIParameters?.XXLCommon?.SubPicHRDParamsPresentFlag ?? false;
            if (!subPicHRDParamsPresentFlag) irapCPBParamsPresentFlag = buffer.ReadBool();
            var auCPBRemovalDelayLengthMinus1 = (byte) (seqParameterSetItem.VUIParameters?.XXLCommon?.AUCPBRemovalDelayLengthMinus1 ?? 23);
            var dpbOutputDelayLengthMinus1 = (byte) (seqParameterSetItem.VUIParameters?.XXLCommon?.DPBOutputDelayLengthMinus1 ?? 23);
            if (irapCPBParamsPresentFlag) buffer.BSSkipBits(auCPBRemovalDelayLengthMinus1 + dpbOutputDelayLengthMinus1 + 2);
            buffer.BSSkipBits(auCPBRemovalDelayLengthMinus1 + 2); //concatenation_flag, au_cpb_removal_delay_delta_minus1
            if (seqParameterSetItem.NalHrdBpPresentFlag)
                SeiMessageBufferingPeriodXXL(buffer, seqParameterSetItem.VUIParameters?.XXLCommon, irapCPBParamsPresentFlag,
                    seqParameterSetItem.VUIParameters?.NAL, payloadSize);
            if (seqParameterSetItem.VclHrdPbPresentFlag)
                SeiMessageBufferingPeriodXXL(buffer, seqParameterSetItem.VUIParameters?.XXLCommon, irapCPBParamsPresentFlag,
                    seqParameterSetItem.VUIParameters?.VCL, payloadSize);
        }

        private static void SeiMessageBufferingPeriodXXL(TSStreamBuffer buffer, XXLCommon xxlCommon, bool irapCPBParamsPresentFlag, XXL xxl, uint payloadSize)
        {
            if (xxlCommon == null || xxl == null)
            {
                buffer.BSSkipBits((int) (payloadSize * 8));
                return;
            }

            for (var schedSelIdx = 0; schedSelIdx < xxl.SchedSel.Count; schedSelIdx++)
            {
                buffer.BSSkipBits(xxlCommon.InitialCPBRemovalDelayLengthMinus1 + 1); //initial_cpb_removal_delay
                buffer.BSSkipBits(xxlCommon.InitialCPBRemovalDelayLengthMinus1 + 1); //initial_cpb_removal_delay_offset
                if (xxlCommon.SubPicHRDParamsPresentFlag || irapCPBParamsPresentFlag)
                {
                    buffer.BSSkipBits(xxlCommon.InitialCPBRemovalDelayLengthMinus1 + 1); //initial_alt_cpb_removal_delay
                    buffer.BSSkipBits(xxlCommon.InitialCPBRemovalDelayLengthMinus1 + 1); //initial_alt_cpb_removal_delay_offset
                }
            }
        }

        // SEI - 1
        private static void SeiMessagePicTiming(TSStreamBuffer buffer, ref uint seqParameterSetID, uint payloadSize)
        {
            if (seqParameterSetID == uint.MaxValue && _seqParameterSets.Count == 1) seqParameterSetID = 0;
            SeqParameterSetStruct seqParameterSetItem;
            if (seqParameterSetID >= _seqParameterSets.Count || (seqParameterSetItem = _seqParameterSets[(int) seqParameterSetID]) == null)
            {
                buffer.BSSkipBits((int) (payloadSize * 8));
                return;
            }

            if (seqParameterSetItem.VUIParameters?.FrameFieldInfoPresentFlag ??
                seqParameterSetItem.GeneralProgressiveSourceFlag && seqParameterSetItem.GeneralInterlacedSourceFlag)
                buffer.BSSkipBits(7); //pic_struct, source_scan_type, duplicate_flag
            if (seqParameterSetItem.CpbDpbDelaysPresentFlag)
            {
                var auCPBRemovalDelayLengthMinus1 = (byte) seqParameterSetItem.VUIParameters.XXLCommon.AUCPBRemovalDelayLengthMinus1;
                var dpbOutputDelayLengthMinus1 = (byte) seqParameterSetItem.VUIParameters.XXLCommon.DPBOutputDelayLengthMinus1;
                var subPicHRDParamsPresentFlag = seqParameterSetItem.VUIParameters.XXLCommon.SubPicHRDParamsPresentFlag;
                buffer.BSSkipBits(auCPBRemovalDelayLengthMinus1 + dpbOutputDelayLengthMinus1 + 2);
                if (subPicHRDParamsPresentFlag)
                {
                    var dpbOutputDelayDULengthMinus1 = (byte) seqParameterSetItem.VUIParameters.XXLCommon.DPBOutputDelayDULengthMinus1;
                    buffer.BSSkipBits(dpbOutputDelayDULengthMinus1 + 1);
                }
            }
        }

        // SEI - 6
        private static void SeiMessageRecoveryPoint(TSStreamBuffer buffer)
        {
            buffer.SkipExp(); //recovery_poc_cnt
            buffer.BSSkipBits(2); //exact_match_flag, broken_link_flag
        }

        // SEI - 129
        private static void SeiMessageActiveParametersSets(TSStreamBuffer buffer)
        {
            buffer.BSSkipBits(6);
            var numSpsIdsMinus1 = buffer.ReadExp();
            buffer.SkipExpMulti((int) (numSpsIdsMinus1 + 1));
        }

        // SEI - 137
        private static void SeiMessageMasteringDisplayColourVolume(TSStreamBuffer buffer)
        {
            uint max, min;
            var x = new ushort[4];
            var y = new ushort[4];

            for (var i = 0; i < 3; i++)
            {
                x[i] = buffer.ReadBits2(16);
                y[i] = buffer.ReadBits2(16);
            }

            x[3] = buffer.ReadBits2(16);
            y[3] = buffer.ReadBits2(16);


            max = buffer.ReadBits4(32);

            // TODO: Doublecheck workaround
            min = buffer.ReadBits4(32) & 0xFFFFFCFF;
            if (min == 0)
            {
                buffer.BSSkipBytes(-3);
                min = buffer.ReadBits4(32) & 0xFFFCFFFF;
            }
            else
            {
                buffer.BSSkipBytes(-4);
                min = buffer.ReadBits4(32);
                if (min > 1000) min = min & 0xFFFCFFFF;
            }

            //Reordering to RGB
            byte R = 4, G = 4, B = 4;
            for (byte c = 0; c < 3; c++)
                if (x[c] < 17500 && y[c] < 17500)
                    B = c;
                else if (y[c] - x[c] >= 0)
                    G = c;
                else
                    R = c;
            if ((R | B | G) >= 4)
            {
                //Order not automaticly detected, betting on GBR order
                G = 0;
                B = 1;
                R = 2;
            }

            _masteringDisplayColorPrimaries = string.Empty;
            var humanReadablePrimaries = false;
            if (x[G] == 15000 && x[B] == 7500 && x[R] == 32000 && x[3] == 15635 && y[G] == 30000 && y[B] == 3000 && y[R] == 16500 && y[3] == 16450)
            {
                _masteringDisplayColorPrimaries = "BT.709";
                humanReadablePrimaries = true;
            }
            else if (x[G] == 8500 && x[B] == 6550 && x[R] == 35400 && x[3] == 15635 && y[G] == 39850 && y[B] == 2300 && y[R] == 14600 && y[3] == 16450)
            {
                _masteringDisplayColorPrimaries = "BT.2020";
                humanReadablePrimaries = true;
            }
            else if (x[G] == 13250 && x[B] == 7500 && x[R] == 34000 && x[3] == 15635 && y[G] == 34500 && y[B] == 3000 && y[R] == 16000 && y[3] == 16450)
            {
                _masteringDisplayColorPrimaries = "Display P3";
                humanReadablePrimaries = true;
            }

            if (!humanReadablePrimaries)
                _masteringDisplayColorPrimaries += string.Format(CultureInfo.InvariantCulture,
                    "R: x={0:0.000000} y={1:0.000000}, G: x={2:0.000000} y={3:0.000000}" +
                    ", B: x={4:0.000000} y={5:0.000000}, White point: x={6:0.000000} y={7:0.000000}", (double) x[R] / 50000, (double) y[R] / 50000,
                    (double) x[G] / 50000, (double) y[G] / 50000, (double) x[B] / 50000, (double) y[B] / 50000, (double) x[3] / 50000, (double) y[3] / 50000);

            _masteringDisplayLuminance = string.Format(CultureInfo.InvariantCulture,
                "min: {0:0.0000} cd/m2, max: " + (max - (int) max == 0 ? "{1:0}" : "{1:0.0000}") + " cd/m2", (double) min / 10000, (double) max / 10000);
        }

        // SEI - 144
        private static void SeiMessageLightLevel(TSStreamBuffer buffer)
        {
            _maximumContentLightLevel = buffer.ReadBits2(16);
            _maximumFrameAverageLightLevel = buffer.ReadBits2(16);
            _lightLevelAvailable = true;
        }

        // SEI - 147
        private static void SeiAlternativeTransferCharacteristics(TSStreamBuffer buffer)
        {
            _preferredTransferCharacteristics = (byte) buffer.ReadBits2(8);
        }

        private static void VUIParameters(TSStreamBuffer buffer, VideoParamSetStruct videoParamSetItem, ref VUIParametersStruct vuiParametersItem)
        {
            var xxlCommon = new XXLCommon();
            XXL nal = new(), vcl = new();

            uint numUnitsInTick = uint.MaxValue, timeScale = uint.MaxValue;
            ushort sarWidth = ushort.MaxValue, sarHeight = ushort.MaxValue;
            byte aspectRatioIDC = 0, videoFormat = 5, videoFullRangeFlag = 0, colourPrimaries = 2, transferCharacteristics = 2, matrixCoefficients = 2;
            var colourDescriptionPresentFlag = false;

            var aspectRatioInfoPresentFlag = buffer.ReadBool();
            if (aspectRatioInfoPresentFlag)
            {
                aspectRatioIDC = (byte) buffer.ReadBits2(8);
                if (aspectRatioIDC == 0xFF)
                {
                    sarWidth = (ushort) buffer.ReadBits4(16);
                    sarHeight = (ushort) buffer.ReadBits4(16);
                }
            }

            if (buffer.ReadBool()) //overscan_info_present_flag
                buffer.BSSkipBits(1);
            var videoSignalTypePresentFlag = buffer.ReadBool();
            if (videoSignalTypePresentFlag)
            {
                videoFormat = (byte) buffer.ReadBits2(3);
                videoFullRangeFlag = (byte) buffer.ReadBits2(1);
                colourDescriptionPresentFlag = buffer.ReadBool();
                if (colourDescriptionPresentFlag)
                {
                    colourPrimaries = (byte) buffer.ReadBits2(8);
                    transferCharacteristics = (byte) buffer.ReadBits2(8);
                    matrixCoefficients = (byte) buffer.ReadBits2(8);
                }
            }

            if (buffer.ReadBool()) //chroma_loc_info_present_flag
            {
                _chromaSampleLocTypeTopField = buffer.ReadExp();
                _chromaSampleLocTypeBottomField = buffer.ReadExp();
            }

            buffer.BSSkipBits(2);
            var frameFieldInfoPresentFlag = buffer.ReadBool();
            if (buffer.ReadBool()) //default_display_window_flag
                buffer.SkipExpMulti(4);
            var timingInfoPresentFlag = buffer.ReadBool();
            if (timingInfoPresentFlag)
            {
                numUnitsInTick = (uint) buffer.ReadBits8(32);
                timeScale = (uint) buffer.ReadBits8(32);
                if (buffer.ReadBool()) //vui_poc_proportional_to_timing_flag
                    buffer.SkipExp();
                if (buffer.ReadBool()) //hrd_parameters_present_flag
                    HRDParameters(buffer, true, videoParamSetItem.VPSMaxSubLayers, ref xxlCommon, ref nal, ref vcl);
            }

            if (buffer.ReadBool()) //bitstream_restriction_flag
            {
                buffer.BSSkipBits(3);
                buffer.SkipExpMulti(5);
            }

            vuiParametersItem = new VUIParametersStruct(nal, vcl, xxlCommon, numUnitsInTick, timeScale, sarWidth, sarHeight, aspectRatioIDC, videoFormat,
                videoFullRangeFlag, colourPrimaries, transferCharacteristics, matrixCoefficients, aspectRatioInfoPresentFlag, videoSignalTypePresentFlag,
                frameFieldInfoPresentFlag, colourDescriptionPresentFlag, timingInfoPresentFlag);
        }

        private static void ShortTermRefPicSets(TSStreamBuffer buffer, uint numShortTermRefPicSets)
        {
            uint numPics = 0;
            for (var stRpsIdx = 0; stRpsIdx < numShortTermRefPicSets; stRpsIdx++)
            {
                var interRefPicSetPredictionFlag = false;
                if (stRpsIdx > 0) interRefPicSetPredictionFlag = buffer.ReadBool();

                if (interRefPicSetPredictionFlag)
                {
                    uint deltaIdxMinus1 = 0;
                    if (stRpsIdx == numShortTermRefPicSets) deltaIdxMinus1 = buffer.ReadExp();
                    if (deltaIdxMinus1 + 1 > stRpsIdx) return;

                    buffer.BSSkipBits(1);
                    buffer.SkipExp();

                    uint numPicsNew = 0;
                    for (uint picPos = 0; picPos <= numPics; picPos++)
                        if (buffer.ReadBool()) //used_by_curr_pic_flag
                        {
                            numPicsNew++;
                        }
                        else
                        {
                            if (buffer.ReadBool()) //use_delta_flag
                                numPicsNew++;
                        }

                    numPics = numPicsNew;
                }
                else
                {
                    var numNegativePics = buffer.ReadExp();
                    var numPositivePics = buffer.ReadExp();
                    numPics = numNegativePics + numPositivePics;
                    for (var i = 0; i < numNegativePics; i++)
                    {
                        buffer.SkipExp();
                        buffer.BSSkipBits(1);
                    }

                    for (var i = 0; i < numPositivePics; i++)
                    {
                        buffer.SkipExp();
                        buffer.BSSkipBits(1);
                    }
                }
            }
        }

        private static void ScalingListData(TSStreamBuffer buffer)
        {
            for (var sizeId = 0; sizeId < 4; sizeId++)
            for (var matrixId = 0; matrixId < (sizeId == 3 ? 2 : 6); matrixId++)
                if (!buffer.ReadBool()) // scaling_list_pred_mode_flag
                {
                    buffer.SkipExp();
                }
                else
                {
                    var coefNum = Math.Min(64, 1 << (4 + (sizeId << 1)));
                    if (sizeId > 1) buffer.SkipExp();
                    for (var i = 0; i < coefNum; i++) buffer.SkipExp();
                }
        }

        private static void ProfileTierLevel(TSStreamBuffer buffer, uint subLayerCount)
        {
            _profileSpace = buffer.ReadBits2(2);
            _tierFlag = buffer.ReadBool();
            _profileIDC = buffer.ReadBits2(5);

            buffer.BSSkipBits(32); // general_profile_compatibility_flags
            buffer.BSSkipBytes(1); // TODO: doublecheck workaround

            _generalProgressiveSourceFlag = buffer.ReadBool();
            _generalInterlacedSourceFlag = buffer.ReadBool();
            buffer.BSSkipBits(1); // general_non_packed_constraint_flag
            _generalFrameOnlyConstraintFlag = buffer.ReadBool();

            buffer.BSSkipBits(44); // general_reserved_zero_44bits

            buffer.BSSkipBytes(2); // TODO: doublecheck workaround

            _levelIDC = buffer.ReadBits2(8);

            var subLayerProfilePresentFlags = new List<bool>();
            var subLayerLevelPresentFlags = new List<bool>();

            for (var subLayerPos = 0; subLayerPos < subLayerCount; subLayerPos++)
            {
                var subLayerProfilePresentFlag = buffer.ReadBool();
                var subLayerLevelPresentFlag = buffer.ReadBool();

                subLayerProfilePresentFlags.Add(subLayerProfilePresentFlag);
                subLayerLevelPresentFlags.Add(subLayerLevelPresentFlag);
            }

            if (subLayerCount > 0) buffer.BSSkipBits(2); //reserved_zero_2bits

            for (var subLayerPos = 0; subLayerPos < subLayerCount; subLayerPos++)
            {
                if (subLayerProfilePresentFlags[subLayerPos]) buffer.BSSkipBits(88); // sub layer profile data
                if (subLayerLevelPresentFlags[subLayerPos]) buffer.BSSkipBits(8); //sub_layer_level_idc
            }
        }

        private static void HRDParameters(TSStreamBuffer buffer, bool commonInfPresentFlag, uint maxNumSubLayersMinus1, ref XXLCommon xxlCommon, ref XXL nal,
            ref XXL vcl)
        {
            byte bitRateScale = 0, cpbSizeScale = 0, duCPBRemovalDelayIncrementLengthMinus1 = 0;
            byte dpbOutputDelayDULengthMinus1 = 0, initialCPBRemovalDelayLengthMinus1 = 0;
            byte auCPBRemovalDelayLengthMinus1 = 0, dpbOutputDelayLengthMinus1 = 0;
            bool nalHRDParametersPresentFlag = false, vclHRDParametersPresentFlag = false;
            var subPicHRDParamsPresentFlag = false;

            if (commonInfPresentFlag)
            {
                nalHRDParametersPresentFlag = buffer.ReadBool();
                vclHRDParametersPresentFlag = buffer.ReadBool();
                if (nalHRDParametersPresentFlag || vclHRDParametersPresentFlag)
                {
                    subPicHRDParamsPresentFlag = buffer.ReadBool();
                    if (subPicHRDParamsPresentFlag)
                    {
                        buffer.BSSkipBits(8); //tick_divisor_minus2
                        duCPBRemovalDelayIncrementLengthMinus1 = (byte) buffer.ReadBits2(5);
                        buffer.BSSkipBits(1); //sub_pic_cpb_params_in_pic_timing_sei_flag
                        dpbOutputDelayDULengthMinus1 = (byte) buffer.ReadBits2(5);
                    }

                    bitRateScale = (byte) buffer.ReadBits2(4);
                    cpbSizeScale = (byte) buffer.ReadBits2(4);
                    if (subPicHRDParamsPresentFlag) buffer.BSSkipBits(4); //cpb_size_du_scale
                    initialCPBRemovalDelayLengthMinus1 = (byte) buffer.ReadBits2(5);
                    auCPBRemovalDelayLengthMinus1 = (byte) buffer.ReadBits2(5);
                    dpbOutputDelayLengthMinus1 = (byte) buffer.ReadBits2(5);
                }
            }

            for (byte numSubLayer = 0; numSubLayer <= maxNumSubLayersMinus1; numSubLayer++)
            {
                uint cpbCntMinus1 = 0;
                bool fixedPicRateWithinCvsFlag = true, lowDelayHRDFlag = false;
                var fixedPicRateGeneralFlag = buffer.ReadBool();
                if (!fixedPicRateGeneralFlag) fixedPicRateWithinCvsFlag = buffer.ReadBool();
                if (fixedPicRateWithinCvsFlag)
                    buffer.SkipExp(); //elemental_duration_in_tc_minus1
                else
                    lowDelayHRDFlag = buffer.ReadBool();
                if (!lowDelayHRDFlag)
                {
                    cpbCntMinus1 = buffer.ReadExp();
                    if (cpbCntMinus1 > 31) return;
                }

                if (nalHRDParametersPresentFlag || vclHRDParametersPresentFlag)
                    xxlCommon = new XXLCommon(subPicHRDParamsPresentFlag, duCPBRemovalDelayIncrementLengthMinus1, dpbOutputDelayDULengthMinus1,
                        initialCPBRemovalDelayLengthMinus1, auCPBRemovalDelayLengthMinus1, dpbOutputDelayLengthMinus1);
                if (nalHRDParametersPresentFlag) SubLayerHrdParameters(buffer, xxlCommon, bitRateScale, cpbSizeScale, cpbCntMinus1, ref nal);
                if (vclHRDParametersPresentFlag) SubLayerHrdParameters(buffer, xxlCommon, bitRateScale, cpbSizeScale, cpbCntMinus1, ref vcl);
            }
        }

        private static void SubLayerHrdParameters(TSStreamBuffer buffer, XXLCommon xxlCommon, byte bitRateScale, byte cpbSizeScale, uint cpbCntMinus1,
            ref XXL hrdParametersItem)
        {
            var schedSel = new List<XXLData>((int) cpbCntMinus1 + 1);
            for (byte schedSelIdx = 0; schedSelIdx <= cpbCntMinus1; ++schedSelIdx)
            {
                var bitRateValueMinus1 = buffer.ReadExp();
                var bitRateValue = (ulong) ((bitRateValueMinus1 + 1) * Math.Pow(2.0, 6 + bitRateScale));
                var cpbSizeValueMinus1 = buffer.ReadExp();
                var cpbSizeValue = (ulong) ((cpbSizeValueMinus1 + 1) * Math.Pow(2.0, 4 + cpbSizeScale));
                if (xxlCommon.SubPicHRDParamsPresentFlag) buffer.SkipExpMulti(2); //cpb_size_du_value_minus1, bit_rate_du_value_minus1
                var cbrFlag = buffer.ReadBool();
                schedSel.Add(new XXLData(bitRateValue, cpbSizeValue, cbrFlag));
            }

            hrdParametersItem = new XXL(schedSel);
        }

        public class VideoParamSetStruct
        {
            public ushort VPSMaxSubLayers;

            public VideoParamSetStruct(ushort vpsMaxSubLayersMinus1)
            {
                VPSMaxSubLayers = vpsMaxSubLayersMinus1;
            }

            public VideoParamSetStruct()
            {
                VPSMaxSubLayers = 0;
            }
        }

        public class XXLData
        {
            public ulong BitRateValue;
            public bool CBRFlag;
            public ulong CPBSizeValue;

            public XXLData(ulong bitRateValue, ulong cpbSizeValue, bool cbrFlag)
            {
                BitRateValue = bitRateValue;
                CPBSizeValue = cpbSizeValue;
                CBRFlag = cbrFlag;
            }
        }

        public class XXL
        {
            public List<XXLData> SchedSel;

            public XXL(List<XXLData> shedSel)
            {
                SchedSel = shedSel;
            }

            public XXL()
            {
                SchedSel = new List<XXLData>();
            }
        }

        public class XXLCommon
        {
            public ushort AUCPBRemovalDelayLengthMinus1;
            public ushort DPBOutputDelayDULengthMinus1;
            public ushort DPBOutputDelayLengthMinus1;
            public ushort DuCPBRemovalDelayIncrementLengthMinus1;
            public ushort InitialCPBRemovalDelayLengthMinus1;
            public bool SubPicHRDParamsPresentFlag;

            public XXLCommon(bool subPicHRDParamsPresentFlag, ushort duCPBRemovalDelayIncrementLengthMinus1, ushort dpbOutputDelayDULengthMinus1,
                ushort initialCPBRemovalDelayLengthMinus1, ushort auCPBRemovalDelayLengthMinus1, ushort dpbOutputDelayLengthMinus1)
            {
                SubPicHRDParamsPresentFlag = subPicHRDParamsPresentFlag;
                DuCPBRemovalDelayIncrementLengthMinus1 = duCPBRemovalDelayIncrementLengthMinus1;
                DPBOutputDelayDULengthMinus1 = dpbOutputDelayDULengthMinus1;
                InitialCPBRemovalDelayLengthMinus1 = initialCPBRemovalDelayLengthMinus1;
                AUCPBRemovalDelayLengthMinus1 = auCPBRemovalDelayLengthMinus1;
                DPBOutputDelayLengthMinus1 = dpbOutputDelayLengthMinus1;
            }

            public XXLCommon()
            {
                SubPicHRDParamsPresentFlag = false;
                DuCPBRemovalDelayIncrementLengthMinus1 = 0;
                DPBOutputDelayDULengthMinus1 = 0;
                InitialCPBRemovalDelayLengthMinus1 = 0;
                AUCPBRemovalDelayLengthMinus1 = 0;
                DPBOutputDelayLengthMinus1 = 0;
            }
        }

        public class VUIParametersStruct
        {
            public byte AspectRatioIDC;
            public bool AspectRatioInfoPresentFlag;
            public bool ColourDescriptionPresentFlag;
            public byte ColourPrimaries;
            public bool FrameFieldInfoPresentFlag;
            public byte MatrixCoefficients;
            public XXL NAL;
            public uint NumUnitsInTick;
            public ushort SarHeight;
            public ushort SarWidth;
            public uint TimeScale;
            public bool TimingInfoPresentFlag;
            public byte TransferCharacteristics;
            public XXL VCL;
            public byte VideoFormat;
            public byte VideoFullRangeFlag;
            public bool VideoSignalTypePresentFlag;
            public XXLCommon XXLCommon;

            public VUIParametersStruct(XXL nal, XXL vcl, XXLCommon xxlCommon, uint numUnitsInTick, uint timeScale, ushort sarWidth, ushort sarHeight,
                byte aspectRatioIDC, byte videoFormat, byte videoFullRangeFlag, byte colourPrimaries, byte transferCharacteristics, byte matrixCoefficients,
                bool aspectRatioInfoPresentFlag, bool videoSignalTypePresentFlag, bool frameFieldInfoPresentFlag, bool colourDescriptionPresentFlag,
                bool timingInfoPresentFlag)
            {
                NAL = nal;
                VCL = vcl;
                XXLCommon = xxlCommon;
                NumUnitsInTick = numUnitsInTick;
                TimeScale = timeScale;
                SarWidth = sarWidth;
                SarHeight = sarHeight;
                AspectRatioIDC = aspectRatioIDC;
                VideoFormat = videoFormat;
                VideoFullRangeFlag = videoFullRangeFlag;
                ColourPrimaries = colourPrimaries;
                TransferCharacteristics = transferCharacteristics;
                MatrixCoefficients = matrixCoefficients;
                AspectRatioInfoPresentFlag = aspectRatioInfoPresentFlag;
                VideoSignalTypePresentFlag = videoSignalTypePresentFlag;
                FrameFieldInfoPresentFlag = frameFieldInfoPresentFlag;
                ColourDescriptionPresentFlag = colourDescriptionPresentFlag;
                TimingInfoPresentFlag = timingInfoPresentFlag;
            }

            public VUIParametersStruct()
            {
                NAL = new XXL();
                VCL = new XXL();
                XXLCommon = new XXLCommon();
                NumUnitsInTick = 0;
                TimeScale = 0;
                SarWidth = 0;
                SarHeight = 0;
                AspectRatioIDC = 0;
                VideoFormat = 0;
                VideoFullRangeFlag = 0;
                ColourPrimaries = 0;
                TransferCharacteristics = 0;
                MatrixCoefficients = 0;
                AspectRatioInfoPresentFlag = false;
                VideoSignalTypePresentFlag = false;
                FrameFieldInfoPresentFlag = false;
                ColourDescriptionPresentFlag = false;
                TimingInfoPresentFlag = false;
            }
        }

        public class SeqParameterSetStruct
        {
            public byte BitDepthChromaMinus8;
            public byte BitDepthLumaMinus8;
            public byte ChromaFormatIDC;
            public uint ConfWinBottomOffset;
            public uint ConfWinLeftOffset;
            public uint ConfWinRightOffset;
            public uint ConfWinTopOffset;
            public bool GeneralFrameOnlyConstraintFlag;
            public bool GeneralInterlacedSourceFlag;
            public bool GeneralProgressiveSourceFlag;
            public uint LevelIDC;
            public byte Log2MaxPicOrderCntLsbMinus4;
            public uint PicHeightInLumaSamples;
            public uint PicWidthInLumaSamples;
            public uint ProfileIDC;
            public uint ProfileSpace;
            public bool SeparateColourPlaneFlag;
            public bool TierFlag;
            public byte VideoParameterSetID;
            public VUIParametersStruct VUIParameters;

            public SeqParameterSetStruct(VUIParametersStruct vuiParameters, uint profileSpace, bool tierFlag, uint profileIDC, uint levelIDC,
                uint picWidthInLumaSamples, uint picHeightInLumaSamples, uint confWinLeftOffset, uint confWinRightOffset, uint confWinTopOffset,
                uint confWinBottomOffset, byte videoParameterSetID, byte chromaFormatIDC, bool separateColourPlaneFlag, byte log2MaxPicOrderCntLsbMinus4,
                byte bitDepthLumaMinus8, byte bitDepthChromaMinus8, bool generalProgressiveSourceFlag, bool generalInterlacedSourceFlag,
                bool generalFrameOnlyConstraintFlag)
            {
                VUIParameters = vuiParameters;
                ProfileSpace = profileSpace;
                TierFlag = tierFlag;
                ProfileIDC = profileIDC;
                LevelIDC = levelIDC;
                PicWidthInLumaSamples = picWidthInLumaSamples;
                PicHeightInLumaSamples = picHeightInLumaSamples;
                ConfWinLeftOffset = confWinLeftOffset;
                ConfWinRightOffset = confWinRightOffset;
                ConfWinTopOffset = confWinTopOffset;
                ConfWinBottomOffset = confWinBottomOffset;
                VideoParameterSetID = videoParameterSetID;
                ChromaFormatIDC = chromaFormatIDC;
                SeparateColourPlaneFlag = separateColourPlaneFlag;
                Log2MaxPicOrderCntLsbMinus4 = log2MaxPicOrderCntLsbMinus4;
                BitDepthLumaMinus8 = bitDepthLumaMinus8;
                BitDepthChromaMinus8 = bitDepthChromaMinus8;
                GeneralProgressiveSourceFlag = generalProgressiveSourceFlag;
                GeneralInterlacedSourceFlag = generalInterlacedSourceFlag;
                GeneralFrameOnlyConstraintFlag = generalFrameOnlyConstraintFlag;
            }

            public SeqParameterSetStruct()
            {
                VUIParameters = new VUIParametersStruct();
                ProfileSpace = 0;
                TierFlag = false;
                ProfileIDC = 0;
                LevelIDC = 0;
                PicWidthInLumaSamples = 0;
                PicHeightInLumaSamples = 0;
                ConfWinLeftOffset = 0;
                ConfWinRightOffset = 0;
                ConfWinTopOffset = 0;
                ConfWinBottomOffset = 0;
                VideoParameterSetID = 0;
                ChromaFormatIDC = 0;
                SeparateColourPlaneFlag = false;
                Log2MaxPicOrderCntLsbMinus4 = 0;
                BitDepthLumaMinus8 = 0;
                BitDepthChromaMinus8 = 0;
                GeneralProgressiveSourceFlag = false;
                GeneralInterlacedSourceFlag = false;
                GeneralFrameOnlyConstraintFlag = false;
            }

            //computed
            public bool NalHrdBpPresentFlag => VUIParameters?.NAL != null;
            public bool VclHrdPbPresentFlag => VUIParameters?.VCL != null;
            public bool CpbDpbDelaysPresentFlag => VUIParameters?.XXLCommon != null;
            public byte ChromaArrayType => SeparateColourPlaneFlag ? (byte) 0 : ChromaFormatIDC;
        }

        public class PicParameterSetStruct
        {
            public bool DependentSliceSegmentsEnabledFlag;
            public byte NumExtraSliceHeaderBits;
            public byte NumRefIdxL0DefaultActiveMinus1;
            public byte NumRefIdxL1DefaultActiveMinus1;
            public byte SeqParameterSetID;

            public PicParameterSetStruct(byte seqParameterSetID, byte numRefIdxL0DefaultActiveMinus1, byte numRefIdxL1DefaultActiveMinus1,
                byte numExtraSliceHeaderBits, bool dependentSliceSegmentsEnabledFlag)
            {
                SeqParameterSetID = seqParameterSetID;
                NumRefIdxL0DefaultActiveMinus1 = numRefIdxL0DefaultActiveMinus1;
                NumRefIdxL1DefaultActiveMinus1 = numRefIdxL1DefaultActiveMinus1;
                NumExtraSliceHeaderBits = numExtraSliceHeaderBits;
                DependentSliceSegmentsEnabledFlag = dependentSliceSegmentsEnabledFlag;
            }

            public PicParameterSetStruct()
            {
                SeqParameterSetID = 0;
                NumRefIdxL0DefaultActiveMinus1 = 0;
                NumRefIdxL1DefaultActiveMinus1 = 0;
                NumExtraSliceHeaderBits = 0;
                DependentSliceSegmentsEnabledFlag = false;
            }
        }

        public class ExtendedDataSet
        {
            public List<string> ExtendedFormatInfo;

            public bool LightLevelAvailable;

            public string MasteringDisplayColorPrimaries;
            public string MasteringDisplayLuminance;
            public uint MaximumContentLightLevel;
            public uint MaximumFrameAverageLightLevel;
            public List<PicParameterSetStruct> PicParameterSets;

            public byte PreferredTransferCharacteristics;
            public List<SeqParameterSetStruct> SeqParameterSets;
            public List<VideoParamSetStruct> VideoParamSets;
            public List<VUIParametersStruct> VUIParameterSets;

            public ExtendedDataSet()
            {
                VideoParamSets = new List<VideoParamSetStruct>();
                VUIParameterSets = new List<VUIParametersStruct>();
                SeqParameterSets = new List<SeqParameterSetStruct>();
                PicParameterSets = new List<PicParameterSetStruct>();

                MasteringDisplayColorPrimaries = string.Empty;
                MasteringDisplayLuminance = string.Empty;
                MaximumContentLightLevel = 0;
                MaximumFrameAverageLightLevel = 0;

                LightLevelAvailable = false;

                ExtendedFormatInfo = new List<string>();

                PreferredTransferCharacteristics = 2;
            }
        }
    }
}