# TaxiReceipt — a Windows Service for late-night taxi receipts

Staff who clock out late at night get a taxi home. TaxiReceipt watches the
attendance-clock database, and when someone punches during the night window
it prints a small 8 × 4 cm slip with their full name and the Persian date and
time, which they hand to the taxi.

It runs as a **Windows Service** (.NET 10 Worker Service): it starts with the
machine, has no UI, is restarted by Windows if it crashes, and logs to the
Windows Event Log.

## Where it came from

The first version was a **WinForms app I wrote in 2015** (.NET Framework 4.5,
Stimulsoft for the slip). It had to stay open on a desktop, and it was still a
prototype with bugs that kept it from working reliably. This version keeps
the same idea and the same database contract, rebuilt as a service with
those bugs fixed:

| 2015 WinForms app | This service |
|---|---|
| A window that had to stay open and logged in | Windows Service: starts at boot, restarts after a crash (SCM recovery actions) |
| Noticed new punches by comparing `COUNT(*)` with the last count, so two punches together printed one slip, and every restart lost the count | Watermark on the punch `Id`, saved to `state.json` after every punch: nothing is missed or printed twice across restarts |
| The month's table name (`C` + Persian yyyyMM) was fixed once at start-up, so after the 1st of the month it kept reading last month's table | Table name worked out on every pass; a new month starts from that table's first row |
| "Until midnight" checked `Hour == 24`, which is never true, so it never switched off | Night window that can cross midnight (default 21:00–06:00); a 02:00 punch belongs to the night before |
| Every timer restart attached another `Tick` handler | One `PeriodicTimer` loop; a failed pass (database or printer down) is logged once and retried |
| The start hour lived in a form field and was lost on exit | Everything in `appsettings.json` |
| A slip for every punch | One slip per person per night (configurable) |

## How it works

```
attendance clock ──► dbo.C140507 (punches, one table per Persian month)
                         │  every PollSeconds: rows with Id > watermark
                         ▼
                  ReceiptProcessor ── in the night window? already printed tonight?
                         │
             ┌───────────┴───────────┐
       PdfReceiptPrinter      WindowsReceiptPrinter
   receipts\<night>_<person>_<id>.pdf   (PrintDocument, 80 × 40 mm)
```

- `NightWindow` — Persian-calendar table names and the time window (pure, unit-tested).
- `ReceiptProcessor` — one polling pass; saves the watermark after each punch,
  so if printing fails halfway, the retry neither reprints nor skips.
- `SqlPunchSource` — the table name changes monthly so it can't be a SQL
  parameter; it is checked against a strict pattern and bracket-quoted.
- Output `Pdf` (default) writes each slip to a folder. Output `Printer` prints
  directly; the printer must be installed for the service's account, and
  Microsoft doesn't officially support System.Drawing printing from a service,
  so PDF is the safe default.

## Run it

Requires the .NET 10 SDK and a local SQL Server.

```powershell
sqlcmd -S localhost -E -i db\setup-demo.sql          # demo Clock database + this month's table
dotnet test                                           # 17 tests (also run by GitHub Actions on every push)
dotnet run --project src\TaxiReceipt.Service          # runs in the console, same code as the service
sqlcmd -S localhost -E -d TaxiReceiptDemo -v PersonId=1001 -i db\punch.sql   # a punch
```

Outside 21:00–06:00 nothing is printed by design; to try it during the day,
set `ActiveFrom` and `ActiveUntil` both to `00:00` (a 24-hour window).

### Install as a Windows Service (elevated PowerShell)

```powershell
.\deploy\install-service.ps1                         # publish to C:\Services\TaxiReceipt, create + start
sqlcmd -S localhost -E -v Db=TaxiReceiptDemo -i db\grant-service-account.sql
Restart-Service TaxiReceipt
.\deploy\uninstall-service.ps1                       # remove it again
```

The service runs as its own virtual account, `NT SERVICE\TaxiReceipt`: no
password, read-only access to the clock database, and write access only to its
install folder. Logs are in Event Viewer → Windows Logs → Application (source
`TaxiReceipt`).

## Settings (`appsettings.json`, section `TaxiReceipt`)

| Setting | Default | |
|---|---|---|
| `ConnectionString` | local `TaxiReceiptDemo`, Windows auth | |
| `TableNamePattern` | `C{0}` | `{0}` = Persian yyyyMM |
| `PollSeconds` | `5` | |
| `ActiveFrom` / `ActiveUntil` | `21:00` / `06:00` | may cross midnight; equal = all day |
| `OncePerPersonPerNight` | `true` | |
| `Output` | `Pdf` | or `Printer` |
| `PdfFolder` / `PrinterName` | `receipts` / default printer | |
| `StateFile` | `state.json` | relative paths are relative to the install folder |
| `Title` | `رسید تاکسی` | printed above the name |

The slip uses Tahoma, which ships with Windows, for Persian text. PDFs are
made with [QuestPDF](https://www.questpdf.com) under its Community license.
