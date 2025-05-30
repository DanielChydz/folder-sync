# Folder Sync – One-Way Directory Synchronization Tool

Folder Sync is a C# console application for one way synchronization of two folders. It's based on incremental verification, and supports MD5 as an option. The project includes basic unit tests and was proofed for edge cases.

## Features

- Synchronizes files and folders from a source folder to a destination folder.
- Optionally verifies file content using MD5 hashes (`-verify`).
- Logs all operations to a specified file.
- Supports pause/resume (SPACE or P) and quit (Q or ESC) at any time.

## Requirements

- .NET 8.0 SDK
- Windows 10 (tested), but should work on any OS supported by .NET 8

## Usage

Import the project to Visual Studio 2022 and build.

**Arguments:**
1. Path to the source directory.
2. Path to the destination directory.
3. Synchronization interval in seconds (5-3600).
4. Path to the log file (.txt).
5. `-verify` – enables full MD5 verification (optional).

**Example:**
```
folder-sync "C:\Source" "C:\Dest" 60 "C:\sync.log" -verify
```
Directory paths have to be inside quotation marks.

## Known issues
1. Folder properties don't get copied (they're created, not copied).
2. Modifying source or destination folder after scanning, but before sync is finished, may result in unexpected behaviour.
3. Console rarely doesn't apply new line when printing logs.
4. When not using `-verify` for MD5, if the file has the same timestamp and size, but different content, it won't be copied/updated.
5. When copying large files, the program becomes unresponsive until the file is fully copied.
