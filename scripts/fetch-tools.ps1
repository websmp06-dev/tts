<#
  Descarga los motores TTS locales a src\TtsVirtualMic\tools:
    - Piper (voces neuronales) + 2 voces en español
    - eSpeak NG (voces robóticas / caricaturescas)
  Cada bloque es independiente: si uno falla, solo avisa y la app sigue funcionando con las demás voces
  (como mínimo con las voces SAPI de Windows).
#>
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\src\TtsVirtualMic\tools'
$tmp  = Join-Path ([IO.Path]::GetTempPath()) 'ttsvm-tools'
New-Item -ItemType Directory -Force -Path $root, $tmp | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# ---- URLs (si alguna cambia, es lo único que hay que tocar) ----
$PiperUrl   = 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip'
$VoiceBase  = 'https://huggingface.co/rhasspy/piper-voices/resolve/main'
$Voices     = @(
  @{ Path = 'es/es_ES/davefx/medium'; Name = 'es_ES-davefx-medium' },
  @{ Path = 'es/es_MX/ald/medium';    Name = 'es_MX-ald-medium'    }
)
$EspeakUrl  = 'https://github.com/espeak-ng/espeak-ng/releases/download/1.52.0/espeak-ng.msi'

# ---- Piper ----
try {
  Write-Host 'Descargando Piper...'
  $zip = Join-Path $tmp 'piper.zip'
  Invoke-WebRequest $PiperUrl -OutFile $zip
  $x = Join-Path $tmp 'piper-x'
  if (Test-Path $x) { Remove-Item $x -Recurse -Force }
  Expand-Archive $zip -DestinationPath $x -Force
  $dest = Join-Path $root 'piper'
  New-Item -ItemType Directory -Force -Path $dest | Out-Null
  $inner = Join-Path $x 'piper'
  if (-not (Test-Path $inner)) { $inner = $x }
  Copy-Item (Join-Path $inner '*') $dest -Recurse -Force

  $vdir = Join-Path $dest 'voices'
  New-Item -ItemType Directory -Force -Path $vdir | Out-Null
  foreach ($v in $Voices) {
    Write-Host "Descargando voz $($v.Name)..."
    foreach ($ext in '.onnx', '.onnx.json') {
      Invoke-WebRequest "$VoiceBase/$($v.Path)/$($v.Name)$ext" -OutFile (Join-Path $vdir "$($v.Name)$ext")
    }
  }
} catch { Write-Warning "Piper no se pudo preparar: $_" }

# ---- eSpeak NG ----
try {
  Write-Host 'Descargando eSpeak NG...'
  $msi = Join-Path $tmp 'espeak-ng.msi'
  Invoke-WebRequest $EspeakUrl -OutFile $msi
  $ex = Join-Path $tmp 'espeak-x'
  if (Test-Path $ex) { Remove-Item $ex -Recurse -Force }
  Start-Process msiexec.exe -ArgumentList "/a `"$msi`" /qn TARGETDIR=`"$ex`"" -Wait
  $exe = Get-ChildItem $ex -Recurse -Filter 'espeak-ng.exe' | Select-Object -First 1
  if (-not $exe) { throw 'espeak-ng.exe no está en el MSI' }
  $dest = Join-Path $root 'espeak'
  New-Item -ItemType Directory -Force -Path $dest | Out-Null
  Copy-Item (Join-Path $exe.DirectoryName '*') $dest -Recurse -Force
  if (-not (Test-Path (Join-Path $dest 'espeak-ng-data'))) { Write-Warning 'Falta la carpeta espeak-ng-data' }
} catch { Write-Warning "eSpeak NG no se pudo preparar: $_" }

Write-Host 'Listo. Contenido de tools:'
Get-ChildItem $root -Recurse -Depth 2 | Select-Object -ExpandProperty FullName
