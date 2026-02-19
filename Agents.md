# AGENTS.md — MySQL Client Show (Windows / C# .NET / Avalonia)

## Obiettivo
Realizzare un'app Windows (desktop) in **C# .NET** con **UI Avalonia** che si colleghi a un server **MySQL** e consenta di monitorare le query eseguite dai client leggendo `mysql.general_log` (log su TABLE) in polling, accodando i risultati in memoria e visualizzandoli in una griglia con filtri (client e ricerca parziale SQL).

---

## Stack tecnico (vincolante)
- Linguaggio: **C#**
- Runtime: **.NET 8**
- UI: **Avalonia**
- Pattern: **MVVM**
- Driver MySQL: **MySqlConnector**
- MVVM helpers: **CommunityToolkit.Mvvm**
- Logging (opzionale): `Microsoft.Extensions.Logging`

---

## Requisiti UI (vincolanti)
Schermata principale con:
1. **TextBox**: `Connection string`
2. **Button**: `Start` -> attiva general log su TABLE e avvia polling
3. **Button**: `Stop` -> spegne general log, svuota `mysql.general_log` e ferma polling
4. **Dropdown**: `Client filter`
5. **TextBox**: `Query search` (ricerca parziale nel testo SQL)
6. **Numeric input**: `Polling interval (ms)` con default `1000`
7. **DataGrid**: risultati (Timestamp, SQL, UserHost)
   - ordinamento di default: `Timestamp` decrescente
8. **Status bar**: stato e conteggi
   - avviso dedicato dopo 1 ora di monitoraggio continuo: la tabella `mysql.general_log` sta crescendo
9. **Icona applicativa** coerente con il dominio MySQL/query monitor

---

## Operativita (vincolante)
1. Utente inserisce la connection string.
2. Premendo **Start**:
   - connette al server
   - esegue:
     - `SET GLOBAL log_output = 'TABLE';`
     - `SET GLOBAL general_log = 'ON';`
   - avvia un loop di polling che legge `mysql.general_log`
   - dopo 1 ora di monitoraggio continuo, mostra un avviso in status bar sulla crescita di `mysql.general_log`
3. Premendo **Stop**:
   - esegue `SET GLOBAL general_log = 'OFF';`
   - esegue `TRUNCATE TABLE mysql.general_log;`
   - ferma il polling e chiude la connessione
4. In uscita applicazione:
   - se il polling e' attivo, viene imposta la procedura di stop prima della chiusura

---

## Configurazione persistente (vincolante)
- La configurazione applicativa viene salvata in file JSON.
- Il file viene **caricato all'avvio** dell'app.
- Il file viene **salvato in uscita** dall'app.
- Percorso: `%LOCALAPPDATA%\MySQLClientShow\appconfig.json`
- Campi persistiti:
  - `ConnectionString`
  - `ClientFilter`
  - `QuerySearchFilter`
  - `PollingIntervalMs`

---

## Query di lettura (base)
```sql
SELECT
  event_time,
  user_host,
  CAST(argument AS CHAR(65535)) AS sql_text
FROM mysql.general_log
WHERE command_type = 'Query'
  AND argument IS NOT NULL
  AND user_host LIKE @userHostLike
  AND event_time >= @fromTime
ORDER BY event_time ASC;
```

---

## Stato implementazione (aggiornato al 2026-02-16)
Implementato e compilabile.

Componenti principali:
- `README.md`: documentazione GitHub (overview, setup, uso, configurazione JSON, note operative).
- `.gitignore`: esclusione artefatti di build (`bin/`, `obj/`) dal versionamento.
- `MySQLClientShow.App/MySQLClientShow.App.csproj`: dipendenze Avalonia, DataGrid, MVVM Toolkit, MySqlConnector, `ApplicationIcon` (Windows), `UseAppHost` e inclusione risorse `Assets`; su macOS crea automaticamente post-build il bundle `MySQLClientShow.App.app` con struttura `Contents` (`MacOS`, `Resources`), `Info.plist` e icona `mysql-client-show.icns`.
- `MySQLClientShow.App/Program.cs`: bootstrap desktop Avalonia; su macOS imposta `MacOSPlatformOptions.DisableDefaultApplicationMenuItems = true` per rimuovere i menu item di default (incluso `About Avalonia`).
- `MySQLClientShow.App/App.axaml` e `MySQLClientShow.App/App.axaml.cs`: tema Fluent, caricamento config JSON in avvio e salvataggio config in uscita; impostazione esplicita del nome applicazione (`MySQL Client Show`) usato dal menu app su macOS al posto del fallback `Avalonia`; configurazione menu macOS con menu applicazione impostato esplicitamente (senza voci di default Avalonia, quindi senza `About Avalonia`) e menu finestra custom `File` che include `Start`, `Stop`, `Clear`, `Export CSV`, `Help / Aiuto (?)` e `Quit MySQL Client Show`, evitando anche la duplicazione di un secondo menu `MySQL Client Show` in barra; impostazione best effort dell'icona Dock via API nativa macOS partendo dal PNG asset.
- `MySQLClientShow.App/Views/MainWindow.axaml`: UI con connection string, Start/Stop, filtro client via dropdown, campo `Query search` per ricerca parziale nel testo SQL, polling interval (`NumericUpDown`), DataGrid, status/count, icona finestra, apertura centrata (`CenterScreen`), doppio click riga e menu contestuale (`Apri dettaglio query`, `Copia query in clipboard`), pulsante `?` per Help.
- `MySQLClientShow.App/Views/MainWindow.axaml.cs`: intercetta la chiusura finestra e forza la procedura di stop polling prima di uscire; gestione doppio click e menu contestuale per aprire il dettaglio query o copiare `SqlText` in clipboard; apertura finestra Help dal pulsante `?`; su macOS imposta l'icona finestra via asset PNG in best effort; imposta il titolo finestra runtime includendo versione programma (`MySQL Client Show - vX.Y.Z.W`); all'apertura della finestra, se le dimensioni richieste superano l'area visibile dello schermo corrente, imposta automaticamente `WindowState = Maximized`; espone metodi riusabili dal menu `File` per richiamare le stesse azioni dei pulsanti (`Start`, `Stop`, `Clear`, `Export CSV`, `Help`).
- `MySQLClientShow.App/Views/HelpWindow.axaml`: finestra Help con contenuti bilingue Italiano/English su funzionamento generale e filtri; note operative aggiornate sul menu app macOS (rimozione `About Avalonia`, menu applicazione senza voci default Avalonia, menu `File` con azioni `Start/Stop/Clear/Export CSV/Help`, chiusura via `File -> Quit MySQL Client Show`, assenza duplicazione menu `MySQL Client Show`, icona Dock esplicitamente impostata, bundle `.app` generato con icona Finder corretta).
- `MySQLClientShow.App/Views/HelpWindow.axaml.cs`: code-behind della finestra Help (chiusura dialog).
- `MySQLClientShow.App/Views/QueryDetailWindow.axaml`: finestra dedicata al dettaglio query (timestamp, client, SQL) con area testo read-only e scrollbar.
- `MySQLClientShow.App/Views/QueryDetailWindow.axaml.cs`: code-behind finestra dettaglio, apertura modal, copia SQL negli appunti, chiusura.
- `MySQLClientShow.App/ViewModels/MainWindowViewModel.cs`: logica MVVM, comandi Start/Stop/Clear, polling asincrono configurabile, filtro client via dropdown (lista popolata dinamicamente dai `user_host` osservati), filtro query testuale parziale case-insensitive (`Contains` su `SqlText`) con supporto multi-termine separato da `|` (match OR), ordinamento default griglia per timestamp decrescente, buffer in memoria scorrevole con cap a 5000 righe (oltre il limite elimina le piu vecchie), deduplica, import/export configurazione, update UI non bloccanti in shutdown; in build `DEBUG` pre-carica 5 record demo all'avvio; migliorata la diagnostica errori avvio polling con messaggi piu espliciti su autenticazione/handshake; i nuovi eventi non coerenti con i filtri attivi vengono scartati in ingresso e non bufferizzati; il comando `Clear` svuota anche la lista `Client filter` riportandola a `(Tutti i client)`; dopo 1 ora di monitoraggio continuo mostra un warning in status bar sulla crescita di `mysql.general_log`; se la connessione cade durante il monitoraggio il polling resta attivo e tenta la riconnessione automatica ogni 2 secondi.
- `MySQLClientShow.App/Utilities/SqlQueryFormatter.cs`: formatter SQL leggero per visualizzare query multi-linea in modo leggibile nella finestra di dettaglio.
- `MySQLClientShow.App/Services/MySqlGeneralLogService.cs`: connessione MySQL, enable/disable general log, `TRUNCATE TABLE mysql.general_log` in stop, query su `mysql.general_log`, normalizzazione connection string (trim virgolette esterne), lettura timestamp server (`CURRENT_TIMESTAMP(6)`), disconnessione esplicita senza `STOP/TRUNCATE` per supportare retry di riconnessione durante polling.
- `MySQLClientShow.App/Services/JsonAppConfigurationStore.cs`: lettura/scrittura configurazione JSON.
- `MySQLClientShow.App/Models/GeneralLogEntry.cs`: DTO righe log.
- `MySQLClientShow.App/Configuration/AppConfiguration.cs`: modello serializzabile della configurazione.
- `MySQLClientShow.App/Assets/mysql-client-show.ico`: icona applicativa principale (EXE + finestra).
- `MySQLClientShow.App/Assets/mysql-client-show.icns`: icona macOS disponibile per packaging/app bundle.
- `MySQLClientShow.App/Assets/mysql-client-show.png`: sorgente raster usata per generare l'icona.

Vincoli polling implementati:
- Default: `1000 ms`
- Minimo: `200 ms`
- Massimo: `60000 ms`
- Comportamento: valori fuori range vengono normalizzati (clamp) nel ViewModel.
- Allineamento temporale iniziale: il `fromTime` iniziale usa il timestamp corrente del server MySQL per ridurre mismatch timezone client/server.

Verifica effettuata:
- `dotnet build MySQLClientShow.sln` -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.App/MySQLClientShow.App.csproj` -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-fix nome menu macOS) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-rimozione `About Avalonia` su macOS) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-fix duplicazione menu `MySQL Client Show` su macOS) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-fix definitivo rimozione `About Avalonia` via menu applicazione custom) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-aggiunta azioni `Start/Stop/Clear/Export CSV/Help` nel menu `File` macOS) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-fix icona Dock macOS) -> successo (0 errori, 0 warning).
- `dotnet build MySQLClientShow.sln` (post-fix icona bundle `.app` macOS in Finder) -> successo (0 errori, 0 warning).
- `plutil -p MySQLClientShow.App/bin/Debug/net8.0/MySQLClientShow.App.app/Contents/Info.plist` -> confermati `CFBundleIconFile = mysql-client-show.icns` e metadati bundle.
- `.github/workflows/publish.yml` (post-fix step `Create Windows ZIP`): aggiunto step PowerShell `Compress-Archive` per creare `MySQLClientShow.zip` su Windows prima dell'upload artifact (in precedenza il file ZIP non veniva mai creato su Windows e l'upload veniva saltato con warning).

---

## Runbook rapido
1. Build:
   - `dotnet build MySQLClientShow.sln`
2. Avvio:
   - `dotnet run --project MySQLClientShow.App`
3. In UI:
   - inserire connection string MySQL
   - impostare `Polling (ms)` (default `1000`, range `200..60000`)
   - premere `Start`
   - monitorare risultati nel DataGrid
   - se la finestra iniziale eccede lo schermo disponibile, viene massimizzata automaticamente
   - se la connessione MySQL si interrompe, il polling non si arresta: l'app tenta la riconnessione automatica ogni 2 secondi e riprende appena disponibile
   - usare `Client filter` (dropdown auto-popolata) per filtrare `UserHost`
   - usare `Query search` per ricerca parziale nel testo SQL (case-insensitive)
   - se il monitoraggio resta attivo per oltre 1 ora, controllare il warning in status bar sulla crescita del log
   - aprire dettaglio query con doppio click su riga oppure con `tasto destro` -> `Apri dettaglio query`
   - copiare rapidamente la query con `tasto destro` -> `Copia query in clipboard`
   - aprire l'help bilingue con il pulsante `?`
   - su macOS, verificare nella barra menu che il menu applicazione mostri `MySQL Client Show` (non `Avalonia`)
   - su macOS, verificare che la voce `About Avalonia` non sia presente
   - su macOS, verificare che il menu applicazione (prima voce) non mostri voci di default Avalonia
   - su macOS, verificare che il menu `File` includa `Start`, `Stop`, `Clear`, `Export CSV` e `Help / Aiuto (?)`, equivalenti ai pulsanti toolbar
   - su macOS, verificare che nel Dock venga mostrata l'icona applicativa (non icona vuota)
   - su macOS, dopo la build verificare il bundle `MySQLClientShow.App/bin/Debug/net8.0/MySQLClientShow.App.app` e l'icona corretta in Finder
   - su macOS, verificare che la chiusura sia disponibile in `File -> Quit MySQL Client Show` e che non compaia un secondo menu `MySQL Client Show`
   - nella finestra dettaglio usare `Copia SQL` per copiare la query formattata
   - premere `Stop` per disattivare `general_log`, svuotare `mysql.general_log` e chiudere sessione
4. Chiusura app:
   - se il polling e' attivo viene eseguito automaticamente `Stop`
   - la configurazione corrente (`ConnectionString`, `ClientFilter`, `QuerySearchFilter`, `PollingIntervalMs`) viene salvata automaticamente su JSON

---

## Prerequisiti MySQL
- Utente con privilegi per eseguire `SET GLOBAL`.
- `log_output = TABLE` consentito.
- Accesso al database `mysql` e alla tabella `mysql.general_log`.
- Privilegio per eseguire `TRUNCATE TABLE mysql.general_log`.

---

## Regola di manutenzione documento
Da questo momento `AGENTS.md` deve essere aggiornato a ogni modifica funzionale o architetturale.

Checklist minima da aggiornare ogni volta:
1. **Stato implementazione** (cosa e' stato aggiunto/modificato)
2. **Verifica** (build/test eseguiti e risultato)
3. **Runbook** (se cambiano comandi o flusso operativo)
4. **Prerequisiti** (se cambiano permessi o dipendenze)

## Regola di manutenzione README
Da questo momento `README.md` deve essere aggiornato a ogni modifica rilevante del progetto.

Checklist minima da aggiornare ogni volta:
1. **Funzionalita** (nuove feature o cambiamenti comportamentali)
2. **Setup/Avvio rapido** (nuovi prerequisiti o comandi)
3. **Configurazione** (nuovi campi/config file/path)
4. **Note operative** (limiti, impatti performance, sicurezza)

## Regola di manutenzione Help
Da questo momento la finestra Help (`MySQLClientShow.App/Views/HelpWindow.axaml`) deve essere mantenuta aggiornata a ogni modifica funzionale rilevante.

Checklist minima da aggiornare ogni volta:
1. **Contenuto Italiano** (flusso operativo, filtri, note)
2. **Contenuto English** (allineato semanticamente alla versione italiana)
3. **Filtri** (comportamento corrente di `Client filter` e `Query search`)
4. **Note operative** (prerequisiti, limiti, messaggi di comportamento)
