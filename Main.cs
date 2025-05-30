namespace folder_sync {
    class Program {
        static void Main(string[] args) {

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
                    "(5) '-verify' for full md5 check (optional)\n" +
                    "Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            bool isInt = int.TryParse(args[2], out int value);
            if (!Directory.Exists(args[0])) {
                Console.WriteLine("Please provide correct source folder path. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            } else if (!Directory.Exists(args[1])) {
                Console.WriteLine("Please provide correct destination folder path. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            } else if (!isInt || int.Parse(args[2]) < 5 || int.Parse(args[2]) > 3600) {
                Console.WriteLine("Please provide correct synchronization delay in seconds, [5-3600] seconds range. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            } else if (args.Count() == 5 && args[4] != "-verify") {
                Console.WriteLine("Please provide correct '-verify' argument. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Globals.sourceFolderPath = args[0];
            Globals.destinationFolderPath = args[1];
            Globals.syncIntervalSec = int.Parse(args[2]);
            Globals.logFilePath = args[3];
            if (args.Count() == 5 && args[4] == "-verify") {
                Globals.doFullMd5Check = true;
                Utils.LogOperation(Operation.INFO, "MD5 verification enabled. Synchronization is going to take longer.");
            }

            bool isOperationSuccessful = false;
            // create log file if doesn't exist
            while (!isOperationSuccessful) {
                try {
                    if (!File.Exists(Globals.logFilePath)) {
                        File.Create(Globals.logFilePath).Dispose();
                        Utils.LogOperation(Operation.CREATE, $"Created log file {Globals.logFilePath}");
                    }
                    isOperationSuccessful = true;
                } catch (Exception ex) {
                    Utils.LogOperation(Operation.FAIL, $"Creating log file failed: {ex.Message} Retrying in 3s.");
                    Thread.Sleep(3000);
                }
            }

            var pauseQuitKeysListener = new Thread(() => {
                while (true) {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape) {
                        Console.WriteLine("Exiting program.");
                        Environment.Exit(0);
                    } else if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.P) {
                        Globals.paused = !Globals.paused;
                        Utils.LogOperation(Operation.INFO, Globals.paused ? "Program paused, press SPACE or P to resume" : "Program resumed, press SPACE or P to pause again.");
                    }
                }
            });
            pauseQuitKeysListener.IsBackground = true;
            pauseQuitKeysListener.Start();

            //
            // main loop
            //
            Utils.LogOperation(Operation.INFO, $"Program started. Press Q or ESC to quit, P or SPACE to pause/resume.");
            while (true) {
                while (Globals.paused) Thread.Sleep(100);
                Utils.LogOperation(Operation.INFO, $"Synchronization started.");

                Globals.updatedSourceFileMap = Utils.ScanFiles(false);
                Globals.destinationFileMap = Utils.ScanFiles(true);
                bool wasSomethingDone = false;

                // process files - copy/update
                foreach (string sourceFilePath in Globals.updatedSourceFileMap.Keys) {
                    while (Globals.paused) Thread.Sleep(100);
                    string relativePath = Path.GetRelativePath(Globals.sourceFolderPath, sourceFilePath);
                    string destFilePath = Path.Combine(Globals.destinationFolderPath, relativePath);

                    if (Utils.ShouldCopyFile(sourceFilePath, destFilePath)) {
                        bool copyResult = Utils.CopyFile(sourceFilePath, destFilePath);
                        if (copyResult && !wasSomethingDone) {
                            wasSomethingDone = true;
                        }
                    }
                }

                // process files - delete
                foreach (string destFilePath in Globals.destinationFileMap.Keys) {
                    while (Globals.paused) Thread.Sleep(100);
                    string relativePath = Path.GetRelativePath(Globals.destinationFolderPath, destFilePath);
                    string sourceFilePath = Path.Combine(Globals.sourceFolderPath, relativePath);

                    if (!Globals.updatedSourceFileMap.ContainsKey(sourceFilePath)) {
                        bool deleteResult = Utils.DeleteFile(destFilePath);
                        if (deleteResult && !wasSomethingDone) {
                            wasSomethingDone = true;
                        }
                    }
                }

                // process empty folders
                bool folderSyncResult = Utils.SyncEmptyFolders();
                if (folderSyncResult && !wasSomethingDone) {
                    wasSomethingDone = true;
                }

                Globals.sourceFileMap = new Dictionary<string, FileData>(Globals.updatedSourceFileMap);

                while (!Globals.isSyncFinished) ;
                if (!wasSomethingDone) Utils.LogOperation(Operation.INFO, "No changes, skipping.");
                Utils.LogOperation(Operation.INFO, $"Waiting {Globals.syncIntervalSec} seconds before next synchronization.");

                int timePassed = 0;
                while (timePassed < Globals.syncIntervalSec * 1000) {
                    if (Globals.paused) {
                        Thread.Sleep(100);
                        continue;
                    }
                    Thread.Sleep(100);
                    timePassed += 100;
                }
            }


        }
    }
}