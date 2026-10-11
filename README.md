# C8E

Ausführbarer klassischer CHIP-8-Emulator in C#/.NET 10 mit OpenTK 4.9.4.
Voraussetzungen: .NET-10-SDK und ein Grafiktreiber mit OpenGL 3.3.
Für hörbaren Ton benötigt OpenTK zusätzlich eine passende native OpenAL-Laufzeit
(unter Windows `openal32.dll`). Ohne sie läuft der Emulator weiter; ♪ im Titel
zeigt einen aktiven Sound-Timer.

## Start

In Visual Studio die Solution `C8E.slnx` öffnen. Der äußere Repository-Ordner bleibt `C8Emu`.

Im Repository-Verzeichnis:

```powershell
dotnet build C8E.slnx
dotnet run --project C8E -- ROMs/key-demo.ch8
dotnet run --project C8E -- ROMs/person.ch8
dotnet run --project C8E -- "C:\Pfad\Spiel.ch8" 700
```

Alternativ die gebaute `C8E.exe` mit ROM-Pfad starten.
Ohne ROM erscheint die Startanleitung; `--help` zeigt ebenfalls Hilfe.
ROMs werden ab Adresse 0x200 geladen und dürfen 2 bis 3584 Bytes enthalten.
Die Demo stammt aus diesem Projekt: Sie zeigt zunächst C, anschließend den
Hexwert jeder gedrückten und losgelassenen CHIP-8-Taste und setzt kurz den Ton-Timer.

Escape beendet das Fenster, F5 lädt die ROM neu.
Standardgeschwindigkeit: 700 Instruktionen pro Sekunde; das zweite Argument
erlaubt 1 bis 100000. Beide Timer laufen unabhängig davon mit 60 Hz.

| CHIP-8 | PC-Tastatur |
| --- | --- |
| 1 2 3 C | 1 2 3 4 |
| 4 5 6 D | Q W E R |
| 7 8 9 E | A S D F |
| A 0 B F | Y X C V |

Z funktioniert ebenfalls für A, damit auch englische Tastaturlayouts passen.
Bei Fokusverlust werden die emulierten Tasten losgelassen.

## Aufbau und Ablauf

- `Program.cs`: Argumente prüfen, ROM lesen, Core und Fenster verbinden.
- `Core/IChip8.cs`: gemeinsame Schnittstelle, ohne OpenTK-Abhängigkeit.
- `Core/Chip8.cs`: 4 KB Speicher, 16 Register, Index, Program Counter,
  16 Einträge im Aufrufstack, 64 × 32 monochrome Pixel, Opcodes und Timer.
- `Core/Chip8Exception.cs`: verständliche Fehler bei ungültigen Opcodes,
  Stackfehlern oder Speicherzugriffen außerhalb des erlaubten Bereichs.
- `Input/Chip8Keyboard.cs`: PC-Tasten auf 16 CHIP-8-Tasten abbilden.
- `Graphics/Chip8Window.cs`: OpenTK-Ereignisschleife, zeitlich geordnete
  CPU-Schritte und Timer-Ticks, OpenGL-Ausgabe und optionaler OpenAL-Ton.

Ein CPU-Schritt liest zwei Bytes an der Program-Counter-Adresse, dekodiert
den Opcode und führt ihn aus. Die Zeichnen-Instruktion verändert nur den
Pixelpuffer. Das Fenster stellt diesen anschließend vergrößert und mit
korrektem Seitenverhältnis dar. Dadurch kann der Core ohne Fenster geprüft werden.

Die ursprünglichen `C8.Application`- und `C8.Core`-Einträge waren leere
Projektplatzhalter; sie wurden durch die oben genannten Ordner ersetzt.

## Bewusste Kompatibilitätsentscheidungen

Die klassischen Opcodes sind implementiert. Bei Varianten gelten:

- 8XY6/8XYE schieben VY nach VX.
- 8XY1/8XY2/8XY3 setzen VF auf 0.
- FX55/FX65 erhöhen I um X + 1; I bleibt 12 Bit breit.
- BNNN springt zu NNN + V0.
- Sprites normalisieren die Startposition modulo 64/32 und werden am Rand
  abgeschnitten. DXY0 ist als Erweiterungsopcode nicht unterstützt.
- FX0A wartet auf Tastendruck und anschließendes Loslassen; die Timer laufen weiter.
- 0NNN-Aufrufe an die historische Host-CPU werden ignoriert.
- Keine SUPER-CHIP-/XO-CHIP-Erweiterungen und kein umschaltbares Quirk-Profil.
- Keine historische VIP-Zyklusgenauigkeit bzw. VBlank-Synchronisierung von DXYN.
  Bei langen Fensterpausen wird maximal 100 ms aufgeholt.

Damit ist das Verhalten definiert; ROMs für andere CHIP-8-Varianten können
abweichende Ergebnisse zeigen.

## Darstellung

Die Farben entsprechen dem Octo-Screenshot: Bildschirmfläche #996600,
aktive Pixel #FFCC00, Außenrand #000000. Ein kleiner Randabstand,
quadratische Pixel und das Seitenverhältnis 2:1 bleiben beim Resize erhalten.

XOR-Sprites werden oft zunächst gelöscht und nach mehreren Instruktionen
neu gezeichnet. Bei 700 Instruktionen/s kann diese Lücke etwa 19 ms dauern;
ein direktes Abbild des CPU-Puffers zeigt dann einzelne leere Bilder.
`Graphics/Chip8Display.cs` hält zuletzt aktive Pixel für 33 ms sichtbar.
Das gilt für alle ROMs und ist ausschließlich eine Darstellungsentscheidung:
CPU-Puffer, XOR-Kollisionen, Geschwindigkeit, Tasten und 60-Hz-Timer bleiben
unverändert. Neue Pixel erscheinen sofort; ausgeschaltete Pixel können
kurz nachleuchten. F5 setzt auch den Darstellungspuffer zurück.
Bewusstes dauerhaftes Löschen wird dadurch bis zu 33 ms verzögert;
bei sehr niedriger CPU-Geschwindigkeit können längere Löschlücken weiterhin
sichtbar sein. Dies ist keine historische VIP-VBlank-Emulation.

## Prüfungen

Die Prüfungen benötigen kein zusätzliches Testframework:

```powershell
dotnet run --project C8E.Checks
dotnet run --project C8E.Checks -- --window
```

40 Core-Prüfungen testen u. a. Arithmetik/Flags, Sprünge, CALL/RET,
klassische Quirks, BCD, Registerspeicher, Spritekollision und Randverhalten,
Tastaturwartebefehle, Timer, Neustart sowie Fehlerfälle. Sechs weitere Prüfungen testen die Nachleuchtdauer, sofortige neue Pixel, Neustart und einen fünfsekündigen XOR-Lösch-/Neuzeichen-Ablauf.
`--window` öffnet zusätzlich ein unsichtbares echtes OpenTK-Fenster und liest
Sprite-, Hintergrund- und Randfarben vor und nach einem Resize zurück; dafür ist ein verfügbarer Grafikdesktop nötig.
Build und beide Prüfmodi wurden erfolgreich ausgeführt. Hörbarer Ton konnte
hier mangels nativer OpenAL-Bibliothek nicht geprüft werden.

## Referenzen

- [OpenTK-Paket](https://www.nuget.org/packages/OpenTK/4.9.4)
- [OpenTK GameWindow-Dokumentation](https://opentk.net/api/OpenTK.Windowing.Desktop.GameWindow.html)

