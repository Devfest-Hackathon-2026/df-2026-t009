param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$editorData = Join-Path $UnityEditor 'Editor\Data'
$runtime = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$framework = Join-Path $editorData 'UnityReferenceAssemblies\unity-4.8-api'
$engine = Join-Path $editorData 'Managed\UnityEngine'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$refs = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += Get-ChildItem -LiteralPath (Join-Path $framework 'Facades') -Filter '*.dll' | ForEach-Object FullName
$refs += Get-ChildItem -LiteralPath $engine -Filter 'UnityEngine*.dll' | ForEach-Object FullName
$output = Join-Path $PSScriptRoot 'SoundContracts.exe'
$responsePath = Join-Path $PSScriptRoot 'sound-contracts.rsp'
$response = @('-nologo', '-target:exe', '-optimize+', '-langversion:9', '-nostdlib+', ('-out:"' + $output + '"'))
$response += $refs | ForEach-Object { '-r:"' + $_ + '"' }
$response += @((Join-Path $taskRoot 'Sound.cs'), (Join-Path $PSScriptRoot 'SoundContracts.cs')) | ForEach-Object { '"' + $_ + '"' }
[System.IO.File]::WriteAllLines($responsePath, $response, [System.Text.UTF8Encoding]::new($false))
& $runtime $compiler ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Sound contract compile failed.' }
$previousMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = $engine
    & $mono $output
    if ($LASTEXITCODE -ne 0) { throw 'Sound contracts failed.' }
} finally {
    $env:MONO_PATH = $previousMonoPath
    Remove-Item -LiteralPath $output, $responsePath -ErrorAction SilentlyContinue
}
