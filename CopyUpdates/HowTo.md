# How to update your games easily?

## How to update your games from DBI shops

- Install [DBI](https://github.com/rashevskyv/dbi/releases) on your Switch
- Open DBI
  - Hold R button while starting a game
  - You are now in HBMenu
  - Open DBI
- Install notUltraNX: https://not.ultranx.ru/en/howto/dbi_repo_setup
- Once installed:
  - Select UltraNX
  - On your phone, use Google Translate camera feature for translation if needed
  - Select 1st option (1. All … \<DIR\>)
  - Wait (10 secs)
  - Press +
  - Select 1st option (Install …)
  - Wait (see progress bar)
  - Press A
  - Wait (it's normal if it's very slow)

---

## How to copy your games/updates to your PC (as backup)

### A) Manually

- Connect your Nintendo Switch to your PC using a USB cable
- Open DBI on your Switch
  - Hold R button while starting a game
  - You are now in HBMenu
  - Open DBI
- Select option "Launch MTP …"
- On your PC, File Explorer → This PC → Switch → "4: Installed games"
- Copy folder by folder

### B) Automated

**First option: using CopyUpdates (recommended)**

```
CopyUpdates.exe -o "\4: Installed games" -d "T:\NintendoGames"
```

**Second option: using mtpdrive & robocopy**

- Install [mtpmount](https://github.com/hst125fan/mtpmount/releases/tag/19.8.0)
- Install Doken
- On your PC:

```
cd "C:\Program Files\MTPdrive"
mtpmount-x64.exe mount Switch "4: Installed games" Y:
robocopy "Y:\" "T:\NintendoGames" /E /XO /MIN:1
```

---

## How to update your games using torrents

First, we need to download updates, then we will upload them on the Switch (see next section).

- Install Switch-Library-Manager
- Copy your `*.keys` in the Switch-Library-Manager folder
- Check the README file and `settings.json` file for more details about the configuration
- Scan your library
- Check the Updates tab
- Check Iptorrent or Torrenday for updates
- When opening the `.torrent`, check that the version number in the filename matches the available version listed in Switch-Library-Manager

---

## How to copy your games/updates to your Switch

### Part A: Unzip downloaded files and organize them

- Install WinRAR
- Unrar all games using PowerShell:
  ```powershell
  cd E:\DownloadedTorrents
  Get-ChildItem -Recurse -File -Filter *.rar |
    Where-Object { $_.DirectoryName -match 'NSW' } |
    ForEach-Object { & "C:\Program Files\WinRAR\Rar.exe" x $_.FullName ($_.DirectoryName + "\") }
  ```
- Move all extracted files into the same folder:
  ```powershell
  Mkdir E:\ExtractedTorrents
  Get-ChildItem -Path E:\DownloadedTorrents -Recurse -File -Include *.nsp,*.xci,*.nsz |
    Move-Item -Destination E:\ExtractedTorrents
  ```
  Alternative via cmd:
  ```
  robocopy E:\DownloadedTorrents E:\ExtractedTorrents *.nsp *.xci *.nsz /S
  ```

**Optional — rename files:**

- Download this tool to rename your files: https://github.com/tetj/ConvertXciToNsp
- More options: https://github.com/tetj/BonusTools/blob/master/documentation/ImportingSwitchNSP.docx
- Convert all XCI to NSP:
  ```
  ConvertXciToNsp.exe -c -d "E:\ExtractedTorrents"
  ```
  - On an SSD, expect about 3 minutes to convert a 16 GB `.xci` file.
  - On an HDD, expect about 8 minutes.
- Convert all NSP into clean file format:
  ```
  ConvertXciToNsp.exe -r "E:\ExtractedTorrents"
  ```

---

### Part B: Upload files

#### Option 1: Manually upload games/updates/DLCs to your Switch

- Open DBI on your Switch
  - Hold R button while starting a game
  - You are now in HBMenu
  - Open DBI
- Select option "Install … DBI Backend"
- Install DBI Backend on your PC
- Open DBI Backend
- Click **Add Folder**
- Select `E:\ExtractedTorrents`
- Click **Select Folder**
- Click **Start Server**
- On your Switch, select the files you want to install by pressing X for each file
- Press A to start
- Press A to confirm
- Wait
- Press B when completed

#### Option 2: Automatically upload all new updates/DLCs to your Switch

```
CopyUpdates.exe -o E:\ExtractedTorrents -d "\5: SD Card install"
```

**Optional — keep your ROMs organized:**

Once everything is copied, instead of deleting `E:\ExtractedTorrents`, you could keep it on your PC as a backup or use it to play with an emulator such as Ryujinx.

If you already had games on your PC in another folder, you can synchronize `E:\` with that folder:

```
CopyUpdates.exe -o E:\ExtractedTorrents -d "T:\NintendoSwitch\ROMs\"
```

---

## How to organize your games/ROMs on your PC

- Use this tool to rename all NSP files to a consistent format: https://github.com/tetj/ConvertXciToNsp
- Other tools: https://github.com/tetj/BonusTools/blob/master/documentation/ImportingSwitchNSP.docx

---

## How to keep track of all your games in one place (Playnite)

- [ImportingSwitchNSP guide](https://github.com/tetj/BonusTools/blob/master/documentation/ImportingSwitchNSP.docx?raw=1)
- [Playnite guide](https://github.com/tetj/BonusTools/blob/master/documentation/Playnite.docx?raw=1)
