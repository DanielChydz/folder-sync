using System.Data.Common;
using System.Security.Cryptography;

var sourceFileMap = new Dictionary<string, FileData>();
var updatedSourceFileMap = new Dictionary<string, FileData>();
var destinationFileMap = new Dictionary<string, FileData>();
int syncIntervalSec = 10;
string? sourceFolderPath;
string? destinationFolderPath;
string? logFilePath;
bool doFullMd5Check = false;

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

sourceFolderPath = args[0];
destinationFolderPath = args[1];
syncIntervalSec = int.Parse(args[2]);
logFilePath = args[3];
if (args.Count() == 5 && args[4] == "-verify") doFullMd5Check = true;

// create log file if doesn't exist
try {
    if (!File.Exists(logFilePath)) {
        File.Create(logFilePath).Dispose();
        LogOperation(Operation.CREATE, $"Created log file {logFilePath}");
    }
} catch (IOException ex) {
    Console.WriteLine(ex.ToString());
}

//
// program code
//


while (true) {
    updatedSourceFileMap = ScanFiles(false);
    destinationFileMap = ScanFiles(true);
    bool wasSomethingDone = false;

    // source -> destination
    foreach (string filePath in updatedSourceFileMap.Keys) {
        string relativePath = Path.GetRelativePath(sourceFolderPath, filePath);
        string targetPath = Path.Combine(destinationFolderPath, relativePath);
        bool shouldCopy = false;

        //if source already contained file
        if (sourceFileMap.ContainsKey(filePath)) {
            // if file timestamp and size are the same
            if (updatedSourceFileMap[filePath].Timestamp == sourceFileMap[filePath].Timestamp && updatedSourceFileMap[filePath].Size == sourceFileMap[filePath].Size) {
                // if destination doesn't contain file OR contains file but size or time differ
                if (!destinationFileMap.ContainsKey(targetPath) || (destinationFileMap[targetPath].Timestamp != sourceFileMap[filePath].Timestamp || destinationFileMap[targetPath].Size != sourceFileMap[filePath].Size)) {
                    shouldCopy = true;
                }
                // if source contained file but timestamp or size differs
            } else {
                shouldCopy = true;
            }
            // if file doesn't exist at destination OR contains file but size or time differ
        } else if (!destinationFileMap.ContainsKey(targetPath) || (destinationFileMap[targetPath].Timestamp != updatedSourceFileMap[filePath].Timestamp || destinationFileMap[targetPath].Size != updatedSourceFileMap[filePath].Size)) {
            shouldCopy = true;
        }

        // copy file
        if (shouldCopy) {
            try {
                if (!Directory.Exists(Path.GetDirectoryName(targetPath)!)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    LogOperation(Operation.CREATE, $"Created folder \"{Path.GetDirectoryName(targetPath)}\".");
                }
                if (!File.Exists(targetPath)) {
                    File.Copy(filePath, targetPath);
                    LogOperation(Operation.COPY, $"Copied file \"{filePath}\" to \"{targetPath}\".");
                } else {
                    File.Copy(filePath, targetPath, true);
                    LogOperation(Operation.UPDATE, $"Updated file \"{targetPath}\".");
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
            if (!wasSomethingDone) wasSomethingDone = true;
        }
    }

    // destination -> source
    foreach (string filePath in destinationFileMap.Keys) {
        string relativePath = Path.GetRelativePath(destinationFolderPath, filePath);
        string targetPath = Path.Combine(sourceFolderPath, relativePath);
        // if doesn't exist in source, but exists in destination
        if (!updatedSourceFileMap.ContainsKey(targetPath)) {
            try {
                File.Delete(filePath);
                LogOperation(Operation.DELETE, $"Deleted file \"{filePath}\".");
                if (!wasSomethingDone) wasSomethingDone = true;
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    sourceFileMap = new Dictionary<string, FileData>(updatedSourceFileMap);

    if (!wasSomethingDone) LogOperation(Operation.LOOP, "No changes, skipping.");
    LogOperation(Operation.LOOP, $"Waiting {syncIntervalSec} seconds before next synchronization.");
    Thread.Sleep(syncIntervalSec * 1000);
}

// log to console and file
void LogOperation(Operation operation, string log) {
    string logMessage = $"[{DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz'Z'")}][{operation}] {log}";

    Console.WriteLine(logMessage);
    try {
        using (StreamWriter logFile = new StreamWriter(logFilePath, true)) {
            logFile.WriteLine(logMessage);
        }
    } catch (IOException ex) {
        Console.WriteLine(ex.ToString());
    }
}

Dictionary<string, FileData> ScanFiles(bool isTargetDestination) {
    var result = new Dictionary<string, FileData>();
    try {
        foreach (string filePath in Directory.GetFiles(args[Convert.ToInt16(isTargetDestination)], "*", SearchOption.AllDirectories)) {
            var fileInfo = new FileInfo(filePath);
            var md5 = "";
            if (doFullMd5Check) md5 = GetMd5(filePath);
            var fileData = new FileData(fileInfo.LastWriteTime, fileInfo.Length, md5);
            result[filePath] = fileData;
        }
    } catch (IOException ex) {
        Console.WriteLine(ex.ToString());
    }
    return result;
}

string GetMd5(string file) {
    string result = "";
    try {
        using (var md5 = MD5.Create()) {
            using (var stream = File.OpenRead(file)) {
                var hash = md5.ComputeHash(stream);
                result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    } catch (Exception ex) {
        Console.WriteLine(ex.ToString());
    }
    if (result == "") LogOperation(Operation.MD5, $"Failed to calculate MD5 for file {file}.");
    return result;
}

enum Operation {
    COPY,
    CREATE,
    UPDATE,
    DELETE,
    MD5,
    LOOP
}
class FileData {
    public DateTime Timestamp { get; set; }
    public long Size { get; set; }
    public string Md5 { get; set; }

    public FileData(DateTime timestamp, long size, string md5) {
        Timestamp = timestamp;
        Size = size;
        Md5 = md5;
    }
}