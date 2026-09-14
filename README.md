# ValheimMMOMOD

<p align="center">
  <img src="https://i.imgur.com/ustEuqj.png" alt="ValheimMMOMOD Logo" width="256" />
</p>

## Descrizione

**ValheimMMOMOD** è un plugin BepInEx per Valheim che migliora l'esperienza multiplayer con funzionalità MMO-style: chat avanzata, comandi personalizzati e integrazione nativa con la console di gioco.

Il plugin intercetta i messaggi della chat tramite hook su `Console` / `Chat` in `Assembly-CSharp.dll`, permettendo:

- Comandi slash personalizzati (`/tp`, `/heal`, `/info`, …)
- Canali di chat separati (globale, party, whisper)
- Logging persistente dei messaggi
- Integrazione con mod server-side esistenti

## Requisiti

| Dipendenza | Versione minima | Note |
|---|---|---|
| Valheim | Latest Steam | Il gioco deve essere aggiornato |
| BepInEx | 5.4.x+ | Framework di modding |
| .NET | 4.8 | Target framework del plugin |

## Installazione

1. Scarica l'ultima release da [Releases](../../releases).
2. Copia `ValheimMMOMOD.dll` nella cartella:
   ```
   Valheim/BepInEx/plugins/
   ```
3. Avvia il gioco: il plugin si carica automaticamente.
4. Verifica nel log BepInEx (`BepInEx/LogOutput.log`) che il plugin sia attivo.

## Configurazione

Il file di configurazione viene generato al primo avvio in:
```
BepInEx/config/ValheimMMOMOD.cfg
```

Opzioni principali:

- **ChatEnabled** — Abilita/disabilita la chat estesa (default: `true`)
- **LogLevel** — Livello di logging (`Debug`, `Info`, `Warning`, `Error`)
- **CustomCommands** — Abilita i comandi slash personalizzati
- **LogToFile** — Salva lo storico della chat su file

## Compilazione da sorgente

```powershell
# Dalla root del repository
dotnet build ValheimMMOMOD.sln -c Release

# La DLL compilata si trova in:
# bin/Release/net48/ValheimMMOMOD.dll
```

Assicurati che i riferimenti ad `Assembly-CSharp.dll` e `BepInEx.dll` puntino alla tua installazione locale di Valheim.

## Struttura del progetto

```
ValheimMMOMOD/
├── src/
│   ├── Plugin.cs          # Entry point BepInEx
│   ├── ChatPatches.cs     # Hook sulla chat
│   ├── Commands/          # Comandi slash
│   └── Config/            # Gestione configurazione
├── logo.png               # Logo del progetto
├── README.md              # Questo file
└── ValheimMMOMOD.csproj   # Progetto .NET
```

## Troubleshooting

| Problema | Soluzione |
|---|---|
| Il plugin non si carica | Verifica che BepInEx sia installato e controlla `LogOutput.log` |
| Comandi non funzionano | Controlla che `CustomCommands=true` nel config |
| Crash all'avvio | Assicurati che le versioni di Valheim e BepInEx siano compatibili |
| Chat duplicata | Disabilita altri mod che modificano la chat |

## Contribuire

1. Fork del repository
2. Crea un branch: `git checkout -b feature/nome-feature`
3. Commit: `git commit -m "feat: descrizione"`
4. Push e apri una Pull Request

## Licenza

MIT License — vedi [LICENSE](LICENSE) per i dettagli.

## Crediti

- Sviluppato per la community di Valheim
- Basato su [BepInEx](https://github.com/BepInEx/BepInEx)
- Ispirato ai migliori mod MMO per Valheim
