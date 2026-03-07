# Guide: Checking Game Updates with DBI (Russian Version)

## 1. Launching DBI
Always launch in **Full Mode** (Black background) to ensure enough memory:
1. Hold **[R]** and launch any game.
2. Select **DBI** from the Homebrew Menu.

## 2. Step-by-Step Update Check
1. **Main Menu:** Scroll to **Инструменты** (Tools) and press **(A)**.
2. **Tools Menu:** Select **Проверка обновлений игр** (Check for game updates) and press **(A)**.
3. DBI will scan and list missing updates/DLC on the screen.

| Russian Text | English Meaning |
| :--- | :--- |
| **Инструменты** | Tools |
| **Проверка обновлений игр** | Check for game updates |
| **Обновить titleDB** | Update Version Database |

## 3. Exporting the List to a File (optional)
DBI automatically records the results of your scan in a log file.
1. Run **Запустить MTP соединение** (MTP Responder) and connect to PC.
2. Navigate to `SD Card > switch > DBI > dbi.log`.
3. Open **dbi.log** to see the list of games and versions that need attention. You can copy this data for your records.

## 4. Highlight Colors (optional)
In the Просмотр установленных игр (Browse apps) list:
- Blue/Light Blue: A newer update is available.
- Red: DLC is missing/available.

## 5. Troubleshooting (Config Fixes)
If the scan fails or doesn't highlight games, check `sdmc:/switch/DBI/dbi.config`:

* **If games aren't highlighted:** Ensure `HighlightUpdates=true`.
* **If the scan gives a network error:** Your `TitleDB` URL is likely dead. Check your dbi.config :
```ini
TitleDB=https://raw.githubusercontent.com/blawar/titledb/master/US.en.json
