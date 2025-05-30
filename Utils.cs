using System.Security.Cryptography;
using folder_sync;

namespace folder_sync {
    public static class Utils {
        static bool isOperationSuccessful = false;
        public static bool ShouldCopyFile(string sourcePath, string destinationPath) {
            FileData sourceFile = Globals.updatedSourceFileMap[sourcePath];

            // If file doesn't exist in destination
            if (!Globals.destinationFileMap.ContainsKey(destinationPath))
                return true;

            var destFile = Globals.destinationFileMap[destinationPath];

            // If sizes differ
            if (sourceFile.Size != destFile.Size)
                return true;

            // If timestamps differ
            if (sourceFile.Timestamp != destFile.Timestamp)
                return true;

            // If MD5 check is enabled and hashes differ
            if (Globals.doFullMd5Check && (sourceFile.Md5 != destFile.Md5)) {
                LogOperation(Operation.INFO, $"MD5 difference: source {sourcePath}, destination {destinationPath}.");
                return true;
            }

            return false;
        }
        public static bool IsDirectoryEmpty(string path) {
            return Directory.GetFiles(path).Length == 0 && Directory.GetDirectories(path).Length == 0;
        }

        public static string GetMd5(string file) {
            string result = "";
            bool isOperationSuccessful = false;
            while (!isOperationSuccessful) {
                try {
                    using (var md5 = MD5.Create()) {
                        using (var stream = File.OpenRead(file)) {
                            var hash = md5.ComputeHash(stream);
                            result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                } catch (Exception ex) {
                    LogOperation(Operation.FAIL, $"Calculating file MD5 failed: {ex.Message}, retrying in 3s.");
                    Thread.Sleep(3000);
                }
                isOperationSuccessful = true;
            }
            return result;
        }

        // log to console and file
        public static void LogOperation(Operation operation, string log) {
            string logMessage = $"[{DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz'Z'")}][{operation}] {log}";

            Console.WriteLine(logMessage);
            bool isOperationSuccessful = false;
            while (!isOperationSuccessful) {
                try {
                    using (StreamWriter logFile = new StreamWriter(Globals.logFilePath, true)) {
                        logFile.WriteLine(logMessage);
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Writing to log file failed: {ex.Message}, retrying in 3s.");
                    Thread.Sleep(3000);
                }
                isOperationSuccessful = true;
            }
        }
    }
}
