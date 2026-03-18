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

# Examples in the command line
```
// Copy all, MTP-mounted Switch -> local drive via DBI
CopyUpdates -o "\4: Installed games" -d "C:\AllMyGames"	

// Copy updates/DLCs, local drive -> MTP-mounted Switch via DBI
CopyUpdates -o "C:\Updates"	-d "\5: SD Card install"

// Copy all, local drive -> MTP-mounted Switch via DBI
CopyUpdates -o "C:\Updates"	-d "\5: SD Card install" -all

// Move all, local drive -> local drive + remove old versions
CopyUpdates -o "C:\Updates" -d "C:\AllMyGames"				
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
  C:\AllMyGames\
  ├── SinglePlayer\
  │   ├── EVERYONE\
  │   ├── TEEN\
  │   └── MATURE\
  └── COOP\
	  ├── EVERYONE\
      ├── TEEN\
```

## File Naming Convention

The utility expects filenames that embed a bracketed ID and optional version, for example:
```
Game Title [0100XXXXXXXX0000][v131072][TYPE].nsp
```
- `[0100XXXXXXXX0000]` — title ID used to match source ↔ destination files.
- `[v131072]` — version number used to decide which copy is newer.
- `[TYPE]` — file type (e.g., BASE, UPD, DLC) used to determine how the file should be handled.

## Advanced features

### `-agerating` — Sort into ESRB / player-count folder tree

Moves game folders into a directory tree organized first by player count (`1` for single-player, `COOP` for multiplayer) and then by ESRB rating (`EVERYONE`, `EVERYONE 10+`, `TEEN`, `MATURE`). Game metadata is looked up in a local copy of [blawar/titledb](https://github.com/blawar/titledb)'s `US.en.json`, which is downloaded automatically (and refreshed if older than 7 days).

```
CopyUpdates -agerating -r -o "C:\gamesFromDbi" -d "C:\AllMyGames"
```

- `-o` is the source directory containing games with the expected filename format : Game Title [0100XXXXXXXX0000][vYYYYYY][TYPE].nsp
- You can use this tool to rename your files correctly : https://github.com/tetj/ConvertXciToNsp
- `-d` is the destination root where the rating tree is built.
- Add `-r` to also scan subfolders of `-o` recursively.
- Resulting structure example:
  ```
  C:\AllMyGames\
  ├── 1\
  │   ├── EVERYONE\
  │   ├── TEEN\
  │   └── MATURE\
  └── COOP\
	  ├── EVERYONE\
	  └── TEEN\
  ```

---

### `-s` / `--sort` — Sort by install status

Connects to a Switch over MTP and reorganizes a local library by whether each game is currently installed.

```
CopyUpdates -s -o "\4: Installed games" -d "C:\AllMyGames"
```

- `-o` must be an MTP path (e.g. `\4: Installed games`) — used to detect which titles are installed.
- `-d` is the local folder to sort.
- Every game file is moved into a `_Installed` or `_NotInstalled` subfolder based on its title ID prefix.
- Empty folders left behind after moving are sent to the Recycle Bin automatically.

```
  C:\AllMyGames\
  ├── 1\
  │   ├── EVERYONE\_Installed
  │   ├── EVERYONE\_NotInstalled\
  │   ├── TEEN\
  │   ├── TEEN\_Installed
  │   ├── TEEN\_NotInstalled\
  │   └── MATURE\
  │   ├── MATURE\_Installed
  │   ├── MATURE\_NotInstalled\
  └── COOP\
  │   ├── EVERYONE\
  │   ├── EVERYONE\_Installed
  │   ├── EVERYONE\_NotInstalled\
  │   ├── TEEN\
  │   ├── TEEN\_Installed
  │   ├── TEEN\_NotInstalled\
```

---

### `-m` / `--match` — Sort a named subfolder by install status
- `-m <name>` — exact folder name to locate (e.g. `_SDCARD1`, `_Backup`).
- All game files inside each matching folder are evaluated; those whose title is not currently installed are moved to a `_NotInstalled` sibling folder.
- Installed games stay where they are.

- **Starting** structure example:
  ```
  C:\AllMyGames\
  ├── 1\
  │   ├── EVERYONE
  │   ├── EVERYONE\_SDCARD1\
  │   ├── TEEN\
  │   ├── TEEN\_SDCARD2\
  └── COOP\
  │   ├── EVERYONE
  │   ├── EVERYONE\_SDCARD1\
  │   ├── TEEN\
  │   ├── TEEN\_SDCARD2\
  ```

- How to use

	First put your first SD card in your Switch, then :
	```
	CopyUpdates -m _SDCARD1 -o "\4: Installed games" -d "C:\AllMyGames"
	```
	First put your second SD card in your Switch, then :
	```
	CopyUpdates -m _SDCARD2 -o "\4: Installed games" -d "C:\AllMyGames"
	```

- **Resulting** structure example:
  ```
  C:\AllMyGames\
  ├── 1\
  │   ├── EVERYONE\_SDCARD1\
  │   ├── EVERYONE\_SDCARD1\_NotInstalled\
  │   ├── TEEN\
  │   ├── TEEN\_SDCARD2\
  │   ├── TEEN\_SDCARD2\_NotInstalled\
  └── COOP\
	  ├── EVERYONE\
  │   ├── EVERYONE\_SDCARD1\
  │   ├── EVERYONE\_SDCARD1\_NotInstalled\
  │   ├── TEEN\
  │   ├── TEEN\_SDCARD2\
  │   ├── TEEN\_SDCARD2\_NotInstalled\
  ```

---

### `-f` / `--flatten` — Flatten game subfolders

Moves base game and update files out of game-specific subfolders into the closest ancestor folder whose name starts with an **underscore** (`_`). DLC files are moved only when there are fewer than 4 DLC files in the same folder; folders with 4 or more DLC files are left untouched.

```
CopyUpdates -f "C:\AllMyGames\_NSPinSubfolders"
```

- Be **careful** with this option, try in a test folder first to make sure it does what you expect.
- It will move files around and delete empty folders, so you might want to have a backup.
- Files that already sit directly inside an underscore-prefixed folder are skipped.
- Empty subfolders left after moving are sent to the Recycle Bin.

- **Starting** structure example:
  ```
  C:\AllMyGames\
  ├── _NSPinSubfolders\
  │   ├── Mario Kart8 Deluxe\
  │   ├── Zelda BOTW\
  │   ├── Dark Souls Remastered\
  ```
- **Resulting** structure example:
  ```
  C:\AllMyGames\
  ├── _NoMoreSubfolders\
  ├── botw.nsp
  ├── botw-update.nsp
  ├── mario_kart8_deluxe.nsp
  ├── dark_souls_remastered.nsp
  ```

---

### `-t` / `--type` — Tag files with missing content-type labels

Appends `[UPD]` or `[DLC]` to any `.nsp`, `.nsz`, or `.xci` filename that is missing a content-type tag. BASE files (title ID ends in `000`) are left unchanged. If `[Update]` is already present it is replaced with the shorter `[UPD]`.

```
CopyUpdates -t "C:\AllMyGames"
```

- The title ID suffix determines the tag: `800` → `[UPD]`, anything else → `[DLC]`.

---

### `-verbose` — Verbose upload logging

Prints the reason every skipped file was not uploaded during an MTP push. Useful for diagnosing why a specific update or DLC is being ignored.

```
CopyUpdates -o "C:\Updates" -d "\5: SD Card install" -verbose
```

---

### `-ulaunch` — Generate uLaunch menu files

Scans a local directory structure and produces the `.m.json` entry files used by [uLaunch](https://github.com/XorTroll/uLaunch) to build custom Switch home-screen menus. Copy the output to `/ulaunch/menu/` on your SD card.

```
CopyUpdates -o "C:\AllMyGames" -d "C:\ulaunch_output" -ulaunch
```

- `-o` is the input folder. Each immediate subfolder becomes a top-level uLaunch folder; nesting is supported.
- Base game `.nsp`/`.nsz` files (title ID ending in `000`) inside each subfolder become game entries (type 1). Updates and DLCs are ignored.
- `-d` is the output folder where the `.m.json` files and mirrored directory tree are written.
- Expected input layout:
  ```
  C:\AllMyGames\
  ├── Zelda Collection\
  │   ├── Zelda BOTW[01007EF00011E000][v0].nsp        ← game entry
  │   └── Zelda BOTW[01007EF00011E800][v196608][UPD].nsp  ← skipped
  └── Coop Collection\
	  └── Mario Kart 8[01237EF00011E123][v0].nsp
  ```
- Generated output layout (mirrors `/ulaunch/menu/`):
  ```
  C:\ulaunch_output\
  ├── 0.m.json                  { "type":3, "name":"Zelda Collection", ... }
  ├── Zelda_Collection\
  │   └── 0.m.json              { "type":1, "application_id":"01007EF00011E000" }
  ├── 1.m.json                  { "type":3, "name":"Coop Collection", ... }
  └── Coop_Collection\
	  └── 0.m.json              { "type":1, "application_id":"01237EF00011E123" }
  ```

---

### sphaira MTP support

In addition to DBI, the tool auto-detects [sphaira](https://github.com/ITotalJustice/sphaira) MTP mode. Paths starting with `This PC\` are treated as MTP paths. When neither DBI nor sphaira is detected, the tool prints an error message.

```
// Pull from sphaira-mounted Switch
CopyUpdates -o "This PC\Switch\Games" -d "C:\AllMyGames"
```

---

### Updated options table

| Flag | Long form | Description |
|------|-----------|-------------|
| `-o` | `--origin` | Origin folder path |
| `-d` | `--destination` | Destination folder path |
| `-c` | `--compare` | Compare mode: report size mismatches and replace differing files |
| `-all` | `--all` | Upload all games to the Switch (including base games not yet installed) |
| `-s` | `--sort` | Sort local games into `_Installed` / `_NotInstalled` using Switch install status (requires `-o` MTP path) |
| `-m <name>` | `--match <name>` | Like `-s` but targets only subfolders named `<name>` under `-d` |
| `-f` | `--flatten` | Move base/update files out of game subfolders into the nearest `_`-prefixed ancestor |
| `-t` | `--type` | Append `[UPD]` or `[DLC]` to filenames missing a content-type tag |
| `-agerating` | `--agerating` | Sort game folders by ESRB rating and player count using titledb metadata |
| `-ulaunch` | `--ulaunch` | Generate uLaunch `.m.json` menu files from a local directory structure |
| `-r` | `--recursive` | Scan origin subfolders recursively (used with `-agerating`) |
| `-verbose` | `--verbose` | Print the reason every skipped file was not uploaded |
| `-h` | `--help` | Show help |

