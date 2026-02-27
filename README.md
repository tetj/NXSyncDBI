# CopyUpdates

A console utility for synchronizing Nintendo Switch game files between an origin folder and a destination folder, especially useful when used with [DBI](https://github.com/rashevskyv/dbi/releases)'s MTP mode.
It's a tool to automate the process of copying games/updates/DLCs between your Switch and your PC.

## Overview

CopyUpdates scans an origin directory for game title subfolders and, for each one that also exists in the destination, 
copies any files whose version (extracted from the filename) is newer than what is already present. 
Files must be identified by their bracketed ID (e.g. `[0100XXXXXXXX0000]`) and 
versioned by their bracketed version tag (e.g. `[v65536]`). 
You can use this tool to fix the file names on your local drives : https://github.com/tetj/ConvertXciToNsp

# Why use this tool ? 
- Keeps your games up to date with the latest updates/DLCs
- Saves time by automating the file transfer process, especially when using DBI's MTP mode
- Ensures that only newer versions of files are copied/stored, preventing unnecessary transfers and saving storage space
- Keep your local drive organized

# How to use this tool ?
- Check [HowTo.md](CopyUpdates/HowTo.md) for instructions

### Examples
```
CopyUpdates.exe :
-o "\4: Installed games" -d "C:\AllMyGames"		// Copy all, from an MTP-mounted Switch to a local drive via DBI
-o "C:\Updates"	-d "\5: SD Card install"		// Copy updates/DLCs, from a local drive to an MTP-mounted Switch via DBI
-o "C:\Updates"	-d "\5: SD Card install" -all	// Copy all, from a local drive to an MTP-mounted Switch via DBI
-o "C:\Updates" -d "C:\AllMyGames"				// Move all, matching from a local drive to another local drive + remove old versions
```

### Options

| Flag | Long form | Description |
|------|-----------|-------------|
| `-o` | `--origin` | Origin folder path |
| `-d` | `--destination` | Destination folder path |
| `-c` | `--compare` | Compare mode: report size mismatches and replace differing files |
| `-all` | `--all` | Upload all games to the Switch (including base games not yet installed) |
| `-h` | `--help` | Show help |

If no arguments are provided the application runs in **interactive mode** and prompts for both paths.

## How It Works

1. **Sync mode** (default) — for every subfolder in the origin that has a matching subfolder in the destination, files are copied when:
   - No file with the same ID exists in the destination, **or**
   - The source file's version number is greater than all destination versions for that ID.
   - Empty source files are skipped.
   - Pre-existing older versions are sent to the Recycle Bin before copying.

2. **Compare mode** (`-c`) — scans every file in the origin, locates its counterpart in the destination by ID, 
and reports a warning when the file sizes differ by more than 3 000 bytes. 
Mismatched files are automatically replaced.

3. **Local drive mode** (auto-detected when neither path starts with `\`)
   — changes the file transfer strategy from stream copy to `File.Move()`.
   - Because the source file is moved (not copied), it is removed from the origin after a successful transfer.
   - MTP mode is used automatically whenever the origin or destination path begins with `\` (e.g. `\4: Installed games`).


## Example

**Origin (before sync):**
```
The Legend of Zelda - Breath of the Wild/
├── The Legend of Zelda - Breath of the Wild[01007EF00011E000][v0].nsp
├── The Legend of Zelda - Breath of the Wild[01007EF00011E800][v196608][UPD].nsp		
└── The Legend of Zelda - Breath of the Wild[01007EF00011F001][DLC].nsp
```
**Destination (before sync):**
```
The Legend of Zelda - Breath of the Wild/
├── The Legend of Zelda - Breath of the Wild[01007EF00011E000][BASE].nsp
└── The Legend of Zelda - Breath of the Wild[01007EF00011E800][v65536][UPD].nsp
```
**Destination (after sync):**
```
The Legend of Zelda - Breath of the Wild/
├── The Legend of Zelda - Breath of the Wild[01007EF00011E000][BASE].nsp
└── The Legend of Zelda - Breath of the Wild[01007EF00011E800][v196608][UPD].nsp // this was updated from v65536 to v196608
└── The Legend of Zelda - Breath of the Wild[01007EF00011F001][DLC].nsp	// this was added from origin
```

----------------------------------------

**Supports any directory structure**
This utility should always find the matching files as long as the title ID is present in the filename, regardless of how the folders are organized.
For example if your destination folder looks like :
```
Zelda Collection/
└──
	The Legend of Zelda - BOTW/
	├── ...
	└── ...
	└── ...
	The Legend of Zelda - TOTK/
	├── ...
	└── ...
	└── ...
	```
Mario Collection/
└── ..
Coop Collection/
└── ..
Puzzles Collection/
└── ..
```

## File Naming Convention

The utility expects filenames that embed a bracketed ID and optional version, for example:
```
Game Title [0100XXXXXXXX0000][v131072][TYPE].nsp
```
- `[0100XXXXXXXX0000]` — title ID used to match source ↔ destination files.
- `[v131072]` — version number used to decide which copy is newer.
- `[TYPE]` — file type (e.g., BASE, UPD, DLC) used to determine how the file should be handled.