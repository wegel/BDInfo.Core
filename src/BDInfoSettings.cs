namespace BDInfo
{
    public static class BDInfoSettings
    {
        public static bool GenerateStreamDiagnostics { get; set; }

        public static bool ExtendedStreamDiagnostics { get; set; }

        public static bool EnableSSIF { get; set; }

        public static bool DisplayChapterCount { get; set; }
        public static bool AutosaveReport { get; set; }
        public static bool GenerateFrameDataFile { get; set; }
        public static bool FilterLoopingPlaylists { get; set; }

        public static bool FilterShortPlaylists { get; set; }

        public static int FilterShortPlaylistsValue { get; set; }

        public static bool UseImagePrefix { get; set; }

        public static string UseImagePrefixValue { get; set; }

        public static bool KeepStreamOrder { get; set; }

        public static bool GenerateTextSummary { get; set; }
        public static string LastPath { get; set; }
    }
}