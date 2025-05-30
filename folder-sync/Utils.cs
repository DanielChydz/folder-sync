using System.Security.Cryptography;

namespace folder_sync {
    public static class Utils {
        public static bool ShouldCopyFile(string sourcePath, string destinationPath) {
            FileData sourceFile = Globals.updatedSourceFileMap[sourcePath];

            // if file doesn't exist in destination
            if (!Globals.destinationFileMap.ContainsKey(destinationPath))
                return true;

            FileData destinationFile = Globals.destinationFileMap[destinationPath];

            // if sizes differ
            if (sourceFile.Size != destinationFile.Size)
                return true;

            // if timestamps differ
            if (sourceFile.Timestamp != destinationFile.Timestamp)
                return true;

            // if MD5 check is enabled and hashes differ
            if (Globals.doFullMd5Check && (sourceFile.Md5 != destinationFile.Md5)) {
                LogOperation(Operation.INFO, $"MD5 difference: source {sourcePath}, destination {destinationPath}.");
                return true;
            }

            return false;
        }

        public static bool CopyFile(string sourcePath, string destinationPath) {
            bool isOperationSuccessful = false;

            while (!isOperationSuccessful) {
                try {
                    string destinationFolder = Path.GetDirectoryName(destinationPath)!;
                    if (!Directory.Exists(destinationFolder)) {
                        Directory.CreateDirectory(destinationFolder);
                        LogOperation(Operation.CREATE, $"Created folder \"{destinationFolder}\".");
                    }

                    bool exists = File.Exists(destinationPath);
                    File.Copy(sourcePath, destinationPath, true);

                    LogOperation(exists ? Operation.UPDATE : Operation.COPY, exists ? $"Updated file \"{destinationPath}\"." : $"Copied file \"{sourcePath}\" to \"{destinationPath}\".");
                    isOperationSuccessful = true;
                } catch (Exception ex) {
                    LogOperation(Operation.FAIL, $"File operation failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }
            return isOperationSuccessful;
        }

        public static bool DeleteFile(string filePath) {
            bool isOperationSuccessful = false;

            while (!isOperationSuccessful) {
                try {
                    File.Delete(filePath);
                    LogOperation(Operation.DELETE, $"Deleted file \"{filePath}\".");
                    isOperationSuccessful = true;
                } catch (Exception ex) {
                    LogOperation(Operation.FAIL, $"Deleting file failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }

            return isOperationSuccessful;
        }

        public static bool SyncEmptyFolders() {
            bool wasSomethingDone = false;

            // copy empty folders from source to destination
            bool isOperationSuccessful = false;
            while (!isOperationSuccessful) {
                try {
                    foreach (string folder in Directory.GetDirectories(Globals.sourceFolderPath, "*", SearchOption.AllDirectories)) {
                        while (Globals.paused) Thread.Sleep(100);
                        if (IsDirectoryEmpty(folder)) {
                            string relativePath = folder.Substring(Globals.sourceFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                            string newFolder = Path.Combine(Globals.destinationFolderPath, relativePath);

                            if (!Directory.Exists(newFolder)) {
                                Directory.CreateDirectory(newFolder);
                                LogOperation(Operation.CREATE, $"Created folder \"{newFolder}\".");
                                wasSomethingDone = true;
                            }
                        }
                    }
                    isOperationSuccessful = true;
                } catch (Exception ex) {
                    LogOperation(Operation.FAIL, $"Creating folder failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }

            // delete empty folders from destination that don't exist in source
            isOperationSuccessful = false;
            while (!isOperationSuccessful) {
                try {
                    foreach (string folder in Directory.GetDirectories(Globals.destinationFolderPath, "*", SearchOption.AllDirectories)) {
                        while (Globals.paused) Thread.Sleep(100);
                        string relativePath = folder.Substring(Globals.destinationFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        string sourceFolder = Path.Combine(Globals.sourceFolderPath, relativePath);

                        if (!Directory.Exists(sourceFolder) && IsDirectoryEmpty(folder)) {
                            Directory.Delete(folder, false);
                            LogOperation(Operation.DELETE, $"Deleted empty folder \"{folder}\".");
                            wasSomethingDone = true;
                        }
                    }
                    isOperationSuccessful = true;
                } catch (Exception ex) {
                    LogOperation(Operation.FAIL, $"Deleting folder failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }

            return wasSomethingDone;
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
                    isOperationSuccessful = true;
                    Globals.isSyncFinished = true;
                } catch (Exception ex) {
                    Globals.isSyncFinished = false;
                    LogOperation(Operation.FAIL, $"Calculating file MD5 failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
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
                    isOperationSuccessful = true;
                    Globals.isSyncFinished = true;
                } catch (Exception ex) {
                    Globals.isSyncFinished = false;
                    Console.WriteLine($"[{DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz'Z'")}][{Operation.FAIL}] Writing to log file failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }
        }

        public static Dictionary<string, FileData> ScanFiles(bool isTargetDestination) {
            var result = new Dictionary<string, FileData>();
            bool isOperationSuccessful = false;
            while (!isOperationSuccessful) {
                try {
                    string path = (isTargetDestination == true) ? Globals.destinationFolderPath : Globals.sourceFolderPath;
                    foreach (string filePath in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) {
                        var fileInfo = new FileInfo(filePath);
                        var md5 = "";
                        if (Globals.doFullMd5Check) md5 = Utils.GetMd5(filePath);
                        var fileData = new FileData(fileInfo.LastWriteTime, fileInfo.Length, md5);
                        result[filePath] = fileData;
                    }
                    isOperationSuccessful = true;
                    Globals.isSyncFinished = true;
                } catch (Exception ex) {
                    Globals.isSyncFinished = false;
                    Utils.LogOperation(Operation.FAIL, $"Getting file info failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }
            return result;
        }
    }
}
