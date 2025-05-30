using folder_sync;

namespace folder_sync {
    public enum Operation {
        COPY,
        CREATE,
        UPDATE,
        DELETE,
        INFO,
        FAIL
    }

    public static class Globals {
        public static Dictionary<string, FileData> sourceFileMap = [];
        public static Dictionary<string, FileData> updatedSourceFileMap = [];
        public static Dictionary<string, FileData> destinationFileMap = [];
        public static List<string> emptySourceFolders = [];
        public static int syncIntervalSec;
        public static string sourceFolderPath = "";
        public static string destinationFolderPath = "";
        public static string logFilePath = "";
        public static bool doFullMd5Check = false;
    }
    
    public class FileData {
        public  DateTime Timestamp { get; set; }
        public long Size { get; set; }
        public string Md5 { get; set; }

        public FileData(DateTime timestamp, long size, string md5) {
            Timestamp = timestamp;
            Size = size;
            Md5 = md5;
        }
    }
}