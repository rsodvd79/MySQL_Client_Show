# AGENT.md — MySQL Client Show (Windows / C# .NET / Avalonia)

## Obiettivo
Realizzare un'app Windows (desktop) in **C# .NET** con **UI Avalonia** che si colleghi a un server **MySQL** e consenta di monitorare le query eseguite dai client leggendo `mysql.general_log` (log su TABLE) in polling, accodando i risultati in memoria e visualizzandoli in una griglia con filtri.

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
3. **Button**: `Stop` -> spegne general log e ferma polling
4. **Dropdown**: `Client filter`
5. **Numeric input**: `Polling interval (ms)` con default `1000`
6. **DataGrid**: risultati (Timestamp, SQL, UserHost)
   - ordinamento di default: `Timestamp` decrescente
7. **Status bar**: stato e conteggi
8. **Icona applicativa** coerente con il dominio MySQL/query monitor

---

## Operativita (vincolante)
1. Utente inserisce la connection string.
2. Premendo **Start**:
   - connette al server
   - esegue:
     - `SET GLOBAL log_output = 'TABLE';`
     - `SET GLOBAL general_log = 'ON';`
   - avvia un loop di polling che legge `mysql.general_log`
3. Premendo **Stop**:
   - esegue `SET GLOBAL general_log = 'OFF';`
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

## Stato implementazione (aggiornato al 2026-02-14)
Implementato e compilabile.

Componenti principali:
- `README.md`: documentazione GitHub (overview, setup, uso, configurazione JSON, note operative).
- `.gitignore`: esclusione artefatti di build (`bin/`, `obj/`) dal versionamento.
- `MySQLClientShow.App/MySQLClientShow.App.csproj`: dipendenze Avalonia, DataGrid, MVVM Toolkit, MySqlConnector, `ApplicationIcon` e inclusione risorse `Assets`.
- `MySQLClientShow.App/Program.cs`: bootstrap desktop Avalonia.
- `MySQLClientShow.App/App.axaml` e `MySQLClientShow.App/App.axaml.cs`: tema Fluent, caricamento config JSON in avvio e salvataggio config in uscita.
- `MySQLClientShow.App/Views/MainWindow.axaml`: UI con connection string, Start/Stop, filtro client via dropdown, polling interval (`NumericUpDown`), DataGrid, status/count, icona finestra, apertura centrata (`CenterScreen`).
- `MySQLClientShow.App/Views/MainWindow.axaml.cs`: intercetta la chiusura finestra e forza la procedura di stop polling prima di uscire.
- `MySQLClientShow.App/ViewModels/MainWindowViewModel.cs`: logica MVVM, comandi Start/Stop/Clear, polling asincrono configurabile, filtro client via dropdown (lista popolata dinamicamente dai `user_host` osservati), ordinamento default griglia per timestamp decrescente, buffer in memoria, deduplica, import/export configurazione, update UI non bloccanti in shutdown.
- `MySQLClientShow.App/Services/MySqlGeneralLogService.cs`: connessione MySQL, enable/disable general log, query su `mysql.general_log`.
- `MySQLClientShow.App/Services/JsonAppConfigurationStore.cs`: lettura/scrittura configurazione JSON.
- `MySQLClientShow.App/Models/GeneralLogEntry.cs`: DTO righe log.
- `MySQLClientShow.App/Configuration/AppConfiguration.cs`: modello serializzabile della configurazione.
- `MySQLClientShow.App/Assets/mysql-client-show.ico`: icona applicativa principale (EXE + finestra).
- `MySQLClientShow.App/Assets/mysql-client-show.png`: sorgente raster usata per generare l'icona.

Vincoli polling implementati:
- Default: `1000 ms`
- Minimo: `200 ms`
- Massimo: `60000 ms`
- Comportamento: valori fuori range vengono normalizzati (clamp) nel ViewModel.

Verifica effettuata:
- `dotnet build MySQLClientShow.sln` -> successo (0 errori, 0 warning).

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
   - usare `Client filter` (dropdown auto-popolata) per filtrare `UserHost`
   - premere `Stop` per disattivare `general_log` e chiudere sessione
4. Chiusura app:
   - se il polling e' attivo viene eseguito automaticamente `Stop`
   - la configurazione corrente (`ConnectionString`, `ClientFilter`, `PollingIntervalMs`) viene salvata automaticamente su JSON

---

## Prerequisiti MySQL
- Utente con privilegi per eseguire `SET GLOBAL`.
- `log_output = TABLE` consentito.
- Accesso al database `mysql` e alla tabella `mysql.general_log`.

---

## Regola di manutenzione documento
Da questo momento `AGENT.md` deve essere aggiornato a ogni modifica funzionale o architetturale.

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
