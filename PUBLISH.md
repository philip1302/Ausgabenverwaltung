# Veröffentlichen

## Automatisch (GitHub Actions)

`.github\workflows\release.yml` läuft bei jedem Push auf `master`. Er
veröffentlicht aber nur dann tatsächlich etwas, wenn `<Version>` in
`Ausgabenverwaltung.csproj` gegenüber dem höchsten vorhandenen
`vX.Y.Z`-Tag gestiegen ist — ein Commit ohne Versionsbump (Bugfix,
Doku, Refactoring) läuft durch den Workflow, löst aber kein Release
aus. Das ist dieselbe Regel wie im Abschnitt "Selbstaktualisierung"
unten, nur automatisiert statt von Hand geprüft.

Ist die Version gestiegen, macht der Workflow auf einem
`windows-latest`-Runner genau das, was hier manuell beschrieben ist:
`dotnet test`, danach `publish.ps1 -Targets win-x64, osx-arm64,
osx-x64` (Linux entfällt, siehe unten), und legt anschließend per
`gh release create` einen Tag `vX.Y.Z` und ein Release mit allen drei
Archiven an — weder Entwurf noch Vorabversion, damit die
Selbstaktualisierung es findet. Ein neues Release veröffentlichen
heißt also nur noch: `<Version>` in der csproj erhöhen, committen,
nach `master` pushen.

Zum lokalen Ausprobieren oder für Ziele, die nicht auf GitHub landen
sollen, bleibt `publish.ps1` direkt aufrufbar:

## Manuell (`publish.ps1`)

Alle vier Ziele entstehen mit einem einzigen Aufruf von `publish.ps1`
im Repository-Wurzelverzeichnis. Jedes Ziel ist eine einzelne,
selbstständig lauffähige Datei (self-contained, PublishSingleFile) -
auf dem Zielrechner wird keine .NET-Runtime vorausgesetzt.

```powershell
.\publish.ps1                         # nur win-x64 (Vorgabe)
.\publish.ps1 -Targets osx-arm64, osx-x64
.\publish.ps1 -All                    # win-x64, linux-x64, osx-arm64, osx-x64
```

Am Ende zeigt das Skript Pfade und Dateigrößen aller erzeugten
Artefakte.

## Ausgabestruktur

```
bin\Release\net10.0\publish\
├── win-x64\
│   ├── Ausgabenverwaltung.exe
│   └── Ausgabenverwaltung-win-x64.zip
├── linux-x64\
│   └── Ausgabenverwaltung
├── osx-arm64\
│   ├── Ausgabenverwaltung.app\
│   └── Ausgabenverwaltung-osx-arm64.tar.gz
└── osx-x64\
    ├── Ausgabenverwaltung.app\
    └── Ausgabenverwaltung-osx-x64.tar.gz
```

**Die Archivnamen sind nicht frei wählbar.** Die Selbstaktualisierung
sucht am Release nach genau diesen Dateinamen
(`Ausgabenverwaltung-win-x64.zip`, `Ausgabenverwaltung-osx-arm64.tar.gz`,
`Ausgabenverwaltung-osx-x64.tar.gz`, siehe
`Ausgabenverwaltung.Core\Updates\UpdatePlatform.cs`). Wer einen davon
ändert, muss ihn dort mitändern — sonst findet die Anwendung beim
Anwender nichts mehr, ohne dass es beim Veröffentlichen auffällt.

Bei den beiden macOS-Zielen entsteht zusätzlich zum App-Bundle ein
`.tar.gz` desselben Bundles - das ist die Datei, die man tatsächlich
weitergibt (siehe Abschnitt macOS unten für den Grund: TAR statt ZIP).
Im `publish`-Ordner selbst bleibt bei macOS keine lose ausführbare
Datei liegen, nur noch das fertige `.app`.

## Windows

`Ausgabenverwaltung.exe` direkt weitergeben und ausführen. Das Symbol
der EXE kommt aus `Assets\AppIcon.ico` (über `<ApplicationIcon>` in
der csproj eingebunden).

## Linux

`Ausgabenverwaltung` (ohne Endung) ist bereits ausführbar markiert,
sofern von diesem Rechner aus direkt weitergegeben - beim Verpacken in
ein ZIP unter Windows geht das Ausführungsrecht allerdings verloren
(siehe macOS-Abschnitt für dieselbe Problematik). Notfalls beim
Anwender einmalig `chmod +x Ausgabenverwaltung`.

## macOS (Apple Silicon und Intel)

### Installieren

1. `Ausgabenverwaltung-osx-arm64.tar.gz` (Apple Silicon: M1/M2/M3/M4)
   bzw. `Ausgabenverwaltung-osx-x64.tar.gz` (Intel) herunterladen.
2. Entpacken - Doppelklick im Finder, oder im Terminal:
   ```
   tar -xzf Ausgabenverwaltung-osx-arm64.tar.gz
   ```
3. Das entstandene `Ausgabenverwaltung.app` nach `/Applications` ziehen.

**Warum TAR und nicht ZIP:** Im `Ausgabenverwaltung.app` steckt in
`Contents/MacOS/Ausgabenverwaltung` die eigentliche ausführbare Datei
- ohne gesetztes Unix-Ausführungsrecht verweigert macOS den Start.
Dieses Bundle entsteht auf einem Windows-Rechner, und NTFS kennt gar
kein Unix-Ausführungsrecht, das ein ZIP mitnehmen könnte. `publish.ps1`
schreibt deshalb das TAR-Format von Hand und trägt den Modus 755 fest
in den Archiveintrag ein (siehe Kommentar am Kopf der Datei) - beim
Entpacken auf dem Mac ist die Datei dadurch bereits ausführbar, ganz
ohne `chmod`.

### Erste Ausführung: Gatekeeper

Das Bundle ist nicht signiert und nicht notarisiert (dafür wäre eine
kostenpflichtige Apple-Developer-Mitgliedschaft nötig) - macOS
verweigert deshalb beim ersten Doppelklick den Start mit einer
Warnung, der Entwickler könne nicht verifiziert werden. Zwei Wege,
das zu umgehen:

- **Einmalig per Rechtsklick:** im Finder auf `Ausgabenverwaltung.app`
  Rechtsklick → Öffnen → im Dialog nochmals "Öffnen" bestätigen. Das
  merkt sich macOS für dieses eine Bundle dauerhaft.
- **Über das Terminal**, für alle, die die Warnung gar nicht erst sehen
  wollen - das Quarantäne-Attribut entfernen, mit dem macOS
  heruntergeladene Dateien markiert:
  ```
  xattr -dr com.apple.quarantine /Applications/Ausgabenverwaltung.app
  ```
  Das `-r` ist hier Pflicht: bei einem Bundle steckt die eigentliche
  ausführbare Datei mehrere Ebenen tief in `Contents/MacOS/`, und ohne
  `-r` entfernt `xattr` das Attribut nur vom `.app`-Ordner selbst, nicht
  vom gesamten Baum darunter - der Start scheitert dann weiterhin.

## Datenablage (Datenbank, Sicherung, Protokoll)

`AppPaths.cs` ermittelt den Anwendungsdatenordner über
`Environment.SpecialFolder.ApplicationData`. Unter Windows ist das
`%APPDATA%`, unter macOS und Linux bildet .NET dasselbe
Sonderverzeichnis aber auf die XDG-Konvention `~/.config` ab - **nicht**
auf das eigentlich für macOS-Apps übliche `~/Library/Application
Support`. Wer auf einem Mac nach den Dateien sucht, findet sie deshalb
hier:

| | Windows | macOS / Linux |
|---|---|---|
| Datenbank | `%APPDATA%\Ausgabenverwaltung\ausgaben.db` | `~/.config/Ausgabenverwaltung/ausgaben.db` |
| Sicherungen | `%APPDATA%\Ausgabenverwaltung\Backups\` | `~/.config/Ausgabenverwaltung/Backups/` |
| Protokoll | `%APPDATA%\Ausgabenverwaltung\Logs\` | `~/.config/Ausgabenverwaltung/Logs/` |
| Einstellungen | `%APPDATA%\Ausgabenverwaltung\settings.json` | `~/.config/Ausgabenverwaltung/settings.json` |

## Symbol (App-Icon)

Die Quelle ist ein einziges, in `tools\New-AppIcon.ps1` gezeichnetes
Segelboot-Motiv, aus dem das Skript beide plattformspezifischen
Formate erzeugt - rein mit .NET/System.Drawing, ohne externe
Werkzeuge:

- `Assets\AppIcon.ico` - über `<ApplicationIcon>` in der csproj in die
  Windows-EXE eingebunden.
- `Assets\AppIcon.icns` - wird von `publish.ps1` beim Bauen der
  macOS-Bundles nach `Contents\Resources\AppIcon.icns` kopiert und in
  der `Info.plist` über `CFBundleIconFile` referenziert.

Beide Dateien sind wie jede andere Bild-Ressource ins Repository
eingecheckt und entstehen nicht bei jedem `publish.ps1`-Lauf neu. Soll
sich das Motiv ändern, `tools\New-AppIcon.ps1` anpassen und einmalig
neu ausführen:

```powershell
.\tools\New-AppIcon.ps1
```

**Falls `Assets\AppIcon.icns` fehlt** (z. B. gelöscht oder bewusst
durch ein professionell gestaltetes Symbol ersetzt, das auf einem
echten Mac mit `iconutil -c icns AppIcon.iconset` aus einem
`.iconset`-Ordner erzeugt wurde - das native macOS-Werkzeug, das unter
Windows nicht zur Verfügung steht): `publish.ps1` baut das Bundle
trotzdem vollständig, nur ohne `CFBundleIconFile` in der `Info.plist`
und ohne Datei in `Contents\Resources\`. macOS zeigt dann das
generische App-Symbol, alles andere bleibt unverändert lauffähig.

## Selbstaktualisierung

Die Anwendung sieht bei jedem Start nach, ob es ein neueres Release gibt
(abschaltbar unter „Verwaltung ▸ Darstellung“). Was dabei zu beachten
ist, wenn veröffentlicht wird:

- **Die Versionsnummer muss steigen.** Verglichen wird `<Version>` aus der
  csproj gegen den Namen der Release-Marke (`v1.2.0`). Ein Release, dessen
  Marke nicht höher ist als die laufende Fassung, löst nichts aus.
- **Kein Vorab-Release und kein Entwurf.** Beides wird übergangen — das ist
  der vorgesehene Weg, etwas zu veröffentlichen, ohne dass es beim Bestand
  ankommt.
- **Die Archive brauchen ihre Prüfsumme.** GitHub berechnet sie selbst und
  liefert sie als `digest` an der Datei mit; ohne sie wird nicht
  ausgetauscht. Zutun ist dafür nichts nötig.
- **Nicht auf `/releases/latest` verlassen.** GitHub führt ein eigenes
  „ist die neueste“-Merkmal, das beim Anlegen gesetzt wird und *nicht*
  nachzieht, wenn ein Vorab-Release später zum regulären erklärt wird.
  Passiert das, hilft:
  ```
  gh api -X PATCH repos/philip1302/Ausgabenverwaltung/releases/<id> -f make_latest=true
  ```
  Die Anwendung selbst ist davon unabhängig — sie holt die ganze Liste und
  nimmt die höchste Version.

- **Der Beschreibungstext des Releases wird dem Anwender gezeigt.** Beim
  ersten Start nach dem Austausch erscheint einmalig die Seite „Was ist
  neu" mit dem Text der übernommenen Veröffentlichung
  (`Core\Updates\ReleaseNotes.cs`). Weil dieser Text bei
  `--generate-notes` aus den **Commit-Betreffs** entsteht, landen sie
  damit vor den Augen des Anwenders — ein Grund mehr für den deutschen
  Aussagesatz. Anmeldename und Adresse hinter jeder Zeile („by @… in
  https://…") werden entfernt; ein Release ohne Text lässt die Seite
  ausfallen, statt sie leer zu zeigen.

Der Austausch selbst passiert nicht im laufenden Betrieb, sondern ganz früh
beim nächsten Start (`Program.Main` → `Core\Updates\UpdateInstaller.cs`),
bevor Datenbank, Fenster und Einzelinstanz-Sperre existieren. Die alte
Fassung bleibt dabei als `.alt` neben der neuen liegen und wird erst beim
übernächsten Start entfernt — wer eine kaputte Fassung erwischt, kann sie
von Hand zurückbenennen.

## Versionsnummer

`<Version>` in `Ausgabenverwaltung.csproj` ist die einzige Stelle, die
gepflegt werden muss - `publish.ps1` liest sie von dort und trägt sie
sowohl in `CFBundleVersion` als auch in
`CFBundleShortVersionString` der macOS-Bundles ein. Dieselbe Zahl
steht auch im Protokoll bei jedem Programmstart und im aufklappbaren
Bereich jedes Fehlerdialogs.
