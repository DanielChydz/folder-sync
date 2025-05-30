using System.Security.Cryptography;
using folder_sync;

namespace folder_sync {
    class Program {

        static void Main(string[] args) {
            bool isOperationSuccessful = false;

            //
            // setup code
            //

            /*
            1st argument = source folder path
            2nd argument = destination folder path
            3rd argument = time interval in seconds
            4th argument = log file path
            */
            if (args.Count() != 4 && args.Count() != 5) {
                Console.WriteLine("Please provide correct arguments:\n " +
                    "(1) source folder path\n " +
                    "(2) destination folder path\n " +
                    "(3) time interval in seconds\n " +
                    "(4) log file path\n " +
                    "(5) '-verify' for full md5 check (optional)");
            }

            Globals.sourceFolderPath = args[0];
            Globals.destinationFolderPath = args[1];
            Globals.syncIntervalSec = int.Parse(args[2]);
            Globals.logFilePath = args[3];
            if (args.Count() == 5 && args[4] == "-verify") {
                Globals.doFullMd5Check = true;
                Utils.LogOperation(Operation.INFO, "MD5 verification enabled. Synchronization is going to take longer.");
            }

            // create log file if doesn't exist
            while (!isOperationSuccessful) {
                try {
                    if (!File.Exists(Globals.logFilePath)) {
                        File.Create(Globals.logFilePath).Dispose();
                        Utils.LogOperation(Operation.CREATE, $"Created log file {Globals.logFilePath}");
                    }
                } catch (Exception ex) {
                    Utils.LogOperation(Operation.FAIL, $"Creating log file failed: {ex.Message}, retrying in 3s.");
                    Thread.Sleep(3000);
                }
                isOperationSuccessful = true;
            }

            //
            // program code
            //

            // main loop
            while (true) {
                Utils.LogOperation(Operation.INFO, $"Synchronization started.");

                Globals.updatedSourceFileMap = ScanFiles(false);
                Globals.destinationFileMap = ScanFiles(true);
                bool wasSomethingDone = false;

                // check files
                foreach (string filePath in Globals.updatedSourceFileMap.Keys) {
                    string relativePath = Path.GetRelativePath(Globals.sourceFolderPath, filePath);
                    string targetPath = Path.Combine(Globals.destinationFolderPath, relativePath);
                    bool shouldCopy = false;

                    //if source already contained file
                    if (Globals.sourceFileMap.ContainsKey(filePath)) {
                        // if file timestamp and size are the same
                        if (!Globals.doFullMd5Check && (Globals.updatedSourceFileMap[filePath].Timestamp == Globals.sourceFileMap[filePath].Timestamp && Globals.updatedSourceFileMap[filePath].Size == Globals.sourceFileMap[filePath].Size)) {
                            // if destination doesn't contain file OR contains file but size or time differ
                            if (!Globals.destinationFileMap.ContainsKey(targetPath) || (Globals.destinationFileMap[targetPath].Timestamp != Globals.sourceFileMap[filePath].Timestamp || Globals.destinationFileMap[targetPath].Size != Globals.sourceFileMap[filePath].Size)) {
                                shouldCopy = true;
                            }
                            // if source contained file but timestamp or size differs
                        } else if (!Globals.doFullMd5Check) {
                            shouldCopy = true;
                            // if md5 verification is enabled
                        } else if (Globals.destinationFileMap.ContainsKey(targetPath)) {
                            if (Globals.updatedSourceFileMap[filePath].Md5 != Globals.destinationFileMap[targetPath].Md5) {
                                shouldCopy = true;
                            }
                        }
                        // if file doesn't exist at destination OR contains file but size or time differ
                    } else if (!Globals.destinationFileMap.ContainsKey(targetPath) || (Globals.destinationFileMap[targetPath].Timestamp != Globals.updatedSourceFileMap[filePath].Timestamp || Globals.destinationFileMap[targetPath].Size != Globals.updatedSourceFileMap[filePath].Size)) {
                        shouldCopy = true;
                    }

                    // copy file
                    if (shouldCopy) {
                        isOperationSuccessful = false;
                        while (!isOperationSuccessful) {
                            try {
                                if (!Directory.Exists(Path.GetDirectoryName(targetPath)!)) {
                                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                                    Utils.LogOperation(Operation.CREATE, $"Created folder \"{Path.GetDirectoryName(targetPath)}\".");
                                }
                                if (!File.Exists(targetPath)) {
                                    File.Copy(filePath, targetPath);
                                    Utils.LogOperation(Operation.COPY, $"Copied file \"{filePath}\" to \"{targetPath}\".");
                                } else {
                                    File.Copy(filePath, targetPath, true);
                                    Utils.LogOperation(Operation.UPDATE, $"Updated file \"{targetPath}\".");
                                }
                            } catch (Exception ex) {
                                Utils.LogOperation(Operation.FAIL, $"Creating folder or copying file failed: {ex.Message}, retrying in 3s.");
                                Thread.Sleep(3000);
                            }
                            isOperationSuccessful = true;
                        }
                        if (!wasSomethingDone) wasSomethingDone = true;
                    }
                }

                // Copy empty folders from source to destination
                while (!isOperationSuccessful) {
                    try {
                        foreach (string folder in Directory.GetDirectories(Globals.sourceFolderPath, "*", SearchOption.AllDirectories)) {
                            if (Utils.IsDirectoryEmpty(folder)) {
                                string relativePath = folder.Substring(Globals.sourceFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                string newFolder = Path.Combine(Globals.destinationFolderPath, relativePath);

                                if (!Directory.Exists(newFolder)) {
                                    Directory.CreateDirectory(newFolder);
                                    Utils.LogOperation(Operation.CREATE, $"Created folder \"{newFolder}\".");
                                    wasSomethingDone = true;
                                }
                            }
                        }
                        isOperationSuccessful = true;
                    } catch (Exception ex) {
                        Utils.LogOperation(Operation.FAIL, $"Creating folder failed: {ex.Message}, retrying in 3s.");
                        Thread.Sleep(3000);
                    }
                }

                // delete files
                foreach (string filePath in Globals.destinationFileMap.Keys) {
                    string relativePath = Path.GetRelativePath(Globals.destinationFolderPath, filePath);
                    string targetPath = Path.Combine(Globals.sourceFolderPath, relativePath);
                    // if doesn't exist in source, but exists in destination
                    if (!Globals.updatedSourceFileMap.ContainsKey(targetPath)) {
                        isOperationSuccessful = false;
                        while (!isOperationSuccessful) {
                            try {
                                File.Delete(filePath);
                                Utils.LogOperation(Operation.DELETE, $"Deleted file \"{filePath}\".");
                                if (!wasSomethingDone) wasSomethingDone = true;
                            } catch (Exception ex) {
                                Utils.LogOperation(Operation.FAIL, $"Deleting folder or file failed: {ex.Message}, retrying in 3s.");
                                Thread.Sleep(3000);
                            }
                            isOperationSuccessful = true;
                        }
                    }
                }

                // empty folder handling
                // copy empty folders
                isOperationSuccessful = false;
                while (!isOperationSuccessful) {
                    try {
                        foreach (string folder in Directory.GetDirectories(Globals.sourceFolderPath, "*", SearchOption.AllDirectories)) {
                            string[] files = Directory.GetFiles(folder);
                            string[] subFolders = Directory.GetDirectories(folder);

                            if (files.Length == 0 && subFolders.Length == 0) {
                                string relativePath = folder.Substring(Globals.sourceFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                string newFolder = Path.Combine(Globals.destinationFolderPath, relativePath);

                                if (!Directory.Exists(newFolder)) {
                                    Directory.CreateDirectory(newFolder);
                                    Utils.LogOperation(Operation.CREATE, $"Created folder \"{Path.GetDirectoryName(newFolder)}\".");
                                }
                            }
                        }
                    } catch (Exception ex) {
                        Utils.LogOperation(Operation.FAIL, $"Creating folder failed: {ex.Message}, retrying in 3s.");
                        Thread.Sleep(3000);
                    }
                    isOperationSuccessful = true;
                }

                // delete empty folders
                // Delete empty folders from destination that don't exist in source
                isOperationSuccessful = false;
                while (!isOperationSuccessful) {
                    try {
                        foreach (string folder in Directory.GetDirectories(Globals.destinationFolderPath, "*", SearchOption.AllDirectories)) {
                            string relativePath = folder.Substring(Globals.destinationFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                            string folderPath = Path.Combine(Globals.sourceFolderPath, relativePath);

                            if (!Directory.Exists(folderPath)) {
                                string[] destFiles = Directory.GetFiles(folder);
                                string[] destSubFolders = Directory.GetDirectories(folder);

                                if (destFiles.Length == 0 && destSubFolders.Length == 0) {
                                    Directory.Delete(folder, false);
                                    Utils.LogOperation(Operation.DELETE, $"Deleted empty folder \"{folder}\".");
                                }
                            }
                        }
                        isOperationSuccessful = true;
                    } catch (Exception ex) {
                        Utils.LogOperation(Operation.FAIL, $"Deleting folder failed: {ex.Message}, retrying in 3s.");
                        Thread.Sleep(3000);
                    }
                }

                Globals.sourceFileMap = new Dictionary<string, FileData>(Globals.updatedSourceFileMap);

                if (!wasSomethingDone) Utils.LogOperation(Operation.INFO, "No changes, skipping.");
                Utils.LogOperation(Operation.INFO, $"Waiting {Globals.syncIntervalSec} seconds before next synchronization.");
                Thread.Sleep(Globals.syncIntervalSec * 1000);
            }


        }

        static Dictionary<string, FileData> ScanFiles(bool isTargetDestination) {
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
                } catch (Exception ex) {
                    Utils.LogOperation(Operation.FAIL, $"Getting file info failed: {ex.Message}, retrying in 3s.");
                    Thread.Sleep(3000);
                }
                isOperationSuccessful = true;
            }
            return result;
        }
    }
}