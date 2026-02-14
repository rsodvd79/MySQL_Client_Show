# MySQL Client Show

Desktop app Windows in **C#/.NET 8 + Avalonia** per monitorare in tempo reale le query MySQL lette da `mysql.general_log` (output su `TABLE`).

![MySQL Client Show Icon](MySQLClientShow.App/Assets/mysql-client-show.png)

## Funzionalita
- Connessione a server MySQL tramite connection string.
- Avvio/arresto monitoraggio con gestione automatica di:
  - `SET GLOBAL log_output = 'TABLE';`
  - `SET GLOBAL general_log = 'ON'/'OFF';`
- Polling configurabile (`Polling interval (ms)`) con vincoli:
  - Default: `1000`
  - Min: `200`
  - Max: `60000`
- Filtro client tramite dropdown (`user_host`) con popolamento dinamico dai dati di polling.
- Griglia risultati con timestamp, user host, SQL.
- Ordinamento di default griglia: `Timestamp` decrescente (record piu recenti in alto).
- Export CSV dei dati visibili in griglia, mantenendo filtro e ordinamento correnti.
- Stato e conteggi in footer.
- Finestra principale centrata automaticamente all'avvio (`CenterScreen`).
- Configurazione persistente in JSON caricata all'avvio e salvata in uscita.
- In chiusura app, se il polling e' attivo viene forzata la procedura di `Stop` prima dell'uscita.

## Stack tecnico
- .NET 8
- Avalonia 11
- CommunityToolkit.Mvvm
- MySqlConnector

## Requisiti
- Windows (app configurata come `WinExe`).
- SDK .NET 8 installato.
- Accesso a un server MySQL/MariaDB con privilegi per:
  - `SET GLOBAL`
  - lettura `mysql.general_log`

## Avvio rapido
```bash
dotnet restore
dotnet build MySQLClientShow.sln
dotnet run --project MySQLClientShow.App
```

## Utilizzo
1. Inserisci la connection string.
2. Imposta il polling (default `1000 ms`).
3. Premi `Start`.
4. Seleziona un client dalla dropdown `Client filter` (opzionale, lista aggiornata automaticamente quando arrivano nuovi client).
5. Premi `Export CSV` per salvare i dati attualmente visibili in griglia (stesso filtro/sort).
6. Premi `Stop` per fermare il monitoraggio e disattivare `general_log`.
7. Se chiudi la finestra con monitoraggio attivo, l'app esegue prima lo stop polling e poi termina.

## Configurazione JSON
La configurazione utente viene persistita in:

`%LOCALAPPDATA%\MySQLClientShow\appconfig.json`

Campi salvati:
- `ConnectionString`
- `ClientFilter`
- `PollingIntervalMs`

Esempio:
```json
{
  "ConnectionString": "Server=127.0.0.1;Port=3306;User ID=root;Password=***;Database=mysql;",
  "ClientFilter": "app_user@10.0.",
  "PollingIntervalMs": 1000
}
```

## Query usata per la lettura log
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

## Note importanti
- `general_log` puo avere impatto sulle performance: usare con criterio, specialmente su ambienti di produzione.
- La connection string e salvata in chiaro nel file JSON locale: proteggi il profilo utente/machine.

## Struttura progetto
```text
MySQLClientShow.sln
MySQLClientShow.App/
  App.axaml
  Program.cs
  Views/MainWindow.axaml
  ViewModels/MainWindowViewModel.cs
  Services/MySqlGeneralLogService.cs
  Services/JsonAppConfigurationStore.cs
  Configuration/AppConfiguration.cs
  Assets/mysql-client-show.ico
```
