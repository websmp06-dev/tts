# TTS Virtual Mic

Convierte texto en voz y lo transmite por un **micrófono virtual** de Windows (VB-CABLE), para usarlo en Discord, Minecraft, Roblox, etc. No captura tu micrófono real: solo se oye el TTS.

## Cómo funciona

```
Texto → motor TTS (Piper / eSpeak NG / voces de Windows) → tono y volumen → CABLE Input
                                                                              │ (driver VB-CABLE)
                                              Discord / juego ← micrófono "CABLE Output"
```

- La app escribe **siempre** en el dispositivo "CABLE Input" (no en el predeterminado de Windows).
- En la otra aplicación elegí como micrófono **CABLE Output**.
- "Escucharme (monitor)" reproduce además por tus auriculares/parlantes para oír lo mismo que los demás.

## Compilar (no hace falta tener nada instalado)

1. Subí esta carpeta a un repositorio de GitHub.
2. En **Actions → Build → Run workflow**.
3. Descargá el artefacto **TtsVirtualMic-Setup** (instalador) o **TtsVirtualMic-portable**.

Compilación local en Windows: instalar .NET 8 SDK e Inno Setup 6, y luego:

```powershell
./scripts/fetch-tools.ps1
dotnet publish src/TtsVirtualMic -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\setup.iss
```

## Instalación del micrófono virtual

El instalador descarga el paquete oficial de VB-Audio, lo instala con permisos de administrador y pide reiniciar. Si no hay Internet, instalá VB-CABLE a mano (https://vb-audio.com/Cable/, `VBCABLE_Setup_x64.exe` como administrador) y reiniciá.

## Si no se oye o no te oyen

Abrí **Diagnóstico** dentro de la app: envía un tono por CABLE Input y mide si llega a CABLE Output. Causas típicas: no reiniciar tras instalar, elegir "CABLE Input" en lugar de "CABLE Output" en Discord, supresión de ruido de Discord, permisos de privacidad del micrófono.

## Personalizar las voces

- eSpeak NG: tabla en `Services/Engines/EspeakEngine.cs` (nombre, variante, tono base).
- Piper: copiá más modelos `.onnx` + `.onnx.json` en `tools/piper/voices/` (https://huggingface.co/rhasspy/piper-voices).
- No se incluyen voces que imiten a personas reales.

## Licencias

- VB-CABLE es *donationware* de VB-Audio: no se redistribuye, se descarga desde su sitio oficial.
- Piper: MIT. eSpeak NG: GPL-3.0 (si distribuís el instalador con eSpeak incluido, respetá esa licencia).
