# Table of Contents

- [How to update your games from DBI shops](#how-to-update-your-games-from-dbi-shops)
- [How to copy your games/updates to your PC (as backup)](#how-to-copy-your-gamesupdates-to-your-pc-as-backup)
  - [A) Manually](#a-manually)
  - [B) Automated](#b-automated)
- [How to update your games using torrents](#how-to-update-your-games-using-torrents)
- [How to copy your games/updates to your Switch](#how-to-copy-your-gamesupdates-to-your-switch)
  - [Part A: Unzip downloaded files and organize them](#part-a-unzip-downloaded-files-and-organize-them)
  - [Part B: Upload files](#part-b-upload-files)
    - [Option 1: Manually upload games/updates/DLCs to your Switch](#option-1-manually-upload-gamesupdatesdlcs-to-your-switch)
    - [Option 2: Automatically upload all new updates/DLCs to your Switch](#option-2-automatically-upload-all-new-updatesdlcs-to-your-switch)
- [How to organize your games/ROMs on your PC](#how-to-organize-your-gamesroms-on-your-pc)
- [How to have your own local DBI shop (OwnFoil alternative)](#how-to-have-your-own-local-dbi-shop-ownfoil-alternative)
- [How to keep track of all your games in one place (Playnite)](#how-to-keep-track-of-all-your-games-in-one-place-playnite)
- [How to use multiple sd cards on your modded Nintendo Switch](#how-to-use-multiple-sd-cards-on-your-modded-switch)
- [How to filter your games by age rating and max number of players](#how-to-filter-your-games-by-age-rating-and-max-number-of-players)


# How to update your games from DBI shops

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
 - Some updates might not be on notUltraNX, you can check for updates using this [guide](https://github.com/tetj/NXSyncDBI/blob/main/CopyUpdates/checking-updates-in-dbi.md)

# How to copy your games/updates to your PC (as backup)

## A) Manually

- Connect your Nintendo Switch to your PC using a USB cable
- Open DBI on your Switch
  - Hold R button while starting a game
  - You are now in HBMenu
  - Open DBI
- Select option "Launch MTP …"
- On your PC, File Explorer → This PC → Switch → "4: Installed games"
- Copy folder by folder

## B) Automated

```
CopyUpdates.exe -o "\4: Installed games" -d "T:\NintendoGames"
```

# How to update your games using torrents

First, we need to download updates, then we will upload them on the Switch (see next section).

- Install [Switch-Library-Manager](https://github.com/giwty/switch-library-manager)
- Use [Lockpick RCM](https://github.com/saneki/Lockpick_RCM) to get your keys if you don't have them already
- Copy your `*.keys` in the Switch-Library-Manager folder
- Check the README file and `settings.json` file for more details about the configuration
- Scan your library
- Check the Updates tab
- Check your favorite websites for updates (such as runtracker, iptorrents, torrentday, Ziperto)
- When opening the `.torrent`, check that the version number in the filename matches the latest available version listed in Switch-Library-Manager

# How to copy your games/updates to your Switch

## Part A: Unzip downloaded files and organize them

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
- [Alternatives](https://github.com/tetj/BonusTools/blob/main/documentation/ImportingSwitchNSP.md#2-rename-your-files-to-match-that-pattern) to rename your files 
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

## Part B: Upload files

### Option 1: Manually upload games/updates/DLCs to your Switch

- Open DBI on your Switch
  - Hold R button while starting a game
  - You are now in HBMenu
  - Open DBI
- Select option "Install … DBI Backend"
- Install [DBI Backend](https://github.com/rashevskyv/dbi/releases) on your PC
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

### Option 2: Automatically upload all new updates/DLCs to your Switch

```
CopyUpdates.exe -o E:\ExtractedTorrents -d "\5: SD Card install"
```

**Optional — keep your ROMs organized:**

Once everything is copied, instead of deleting `E:\ExtractedTorrents`, you could keep it on your PC as a backup or use it to play with an emulator such as Ryujinx.

If you already had games on your PC in another folder, you can synchronize `E:\` with that folder:

```
CopyUpdates.exe -o E:\ExtractedTorrents -d "T:\NintendoSwitch\ROMs\"
```

# How to organize your games/ROMs on your PC

- Use this tool to rename all NSP files to a consistent format: https://github.com/tetj/ConvertXciToNsp

# How to have your own local DBI shop (OwnFoil alternative)
You should get 11MB/sec transfer speed. If you don't, try forcing 5GHz Wi-Fi (on your router).
### Option 1 : Using python (much simpler than SGS imho)
On your Switch SD card edit: /switch/dbi/dbi.locations

Add entries pointing to your server.

Example:
```
[SwitchLibrary]
Type=ApacheHTTP
Url=http://192.168.1.15:8030/
```
Then on your PC :
```
cd C:/Nintendo/MyGames
python -m http.server 8030
```

### Option 2 : Using SGS
- Use this guide : https://github.com/notf0und/SGS

### Note about the file organization :
- You will have to put your NSP files in the SGS **/games** folder. 
- If you don't want to move your files there for some reasons, I suggest installing SGS in the parent folder of your games folder.
- So you would do something like this :
```
cd "C:/Nintendo/MyGames"
git clone https://github.com/notf0und/SGS
cd SGS
move your files to parent folder
cd ..
rmdir SGS
```

Then editing C:\Nintendo\docker.compose.yml :
```
    volumes:
      - ./MyGames:/app/www/storage/app/public/games
```
- Don't forget the following command to apply the changes :
```
docker compose up -d
```

# How to keep track of all your games in one place (Playnite)

- [Importing Switch NSP guide](https://github.com/tetj/BonusTools/blob/main/documentation/ImportingSwitchNSP.md)
- [Playnite guide](https://github.com/tetj/BonusTools/blob/main/documentation/Playnite.docx?raw=1)

# How to use multiple sd cards on your modded Switch

To keep your 2-3 cards setup organized :

1. Format the new cards to FAT32 or NTFS.
2. Copy these folders from your **original card** to every new one:
    - /atmosphere
    - /bootloader
    - /switch
    - loose files such as hbmenu.nro and payload.bin
    - do **not** copy /emummc
    - do **not** copy /Nintendo
3. Create a new emuMMC on the **second card**
    - Insert the second SD card.
    - Boot into Hekate.
    - Go to : emuMMC -> Create emuMMC -> SD File
4. Boot emuMMC/Atmosphere (the Switch will create a new Nintendo folder)
5. Cleanup
    - Use DBI's "Cleanup" feature after swapping cards to remove any orphaned entries if you find the Home Screen getting cluttered with broken icons.
    - You might need to remove games previously installed using DBI : **Просмотр установленных игр** (Browse installed applications)  -> then X -> then A
6. Install games on the **second card**
7. Result :
```
SD CARD A
  /atmosphere
  /bootloader
  /emuMMC
  /Nintendo  ← games for library A

SD CARD B
  /atmosphere
  /bootloader
  /emuMMC
  /Nintendo  ← games for library B
```

8.  Warning on Wear and Tear

    The Switch’s SD card slot is a "daughterboard" connected by a somewhat fragile ribbon cable. It is not designed for thousands of swaps. 
    If you plan to swap these 3 cards daily, you might consider eventually upgrading to a single 1.5TB or 2TB card to save the hardware from physical wear.

# How to filter your games by age rating and max number of players
- Get latest release here : https://github.com/tetj/sphaira/releases/
- Copy the .nro on your SD card /switch folder
- Hold R button while starting a game
- Click on Sphaira
- Press Y -> Menu -> Games
- Press X -> Filter By -> the choice is yours

