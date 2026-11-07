param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$assets = Split-Path -Parent $taskRoot
$editorData = Join-Path $UnityEditor 'Editor\Data'
$runtime = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$framework = Join-Path $editorData 'UnityReferenceAssemblies\unity-4.8-api'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$refs = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += Get-ChildItem -LiteralPath (Join-Path $framework 'Facades') -Filter '*.dll' | ForEach-Object FullName
$refs += Join-Path $editorData 'Managed\ExCSS.Unity.dll'
$output = Join-Path $PSScriptRoot 'CssSyntaxCheck.exe'
$args = @('-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib+', ('-out:' + $output))
$args += $refs | ForEach-Object { '-r:' + $_ }
$args += Join-Path $PSScriptRoot 'CssSyntaxCheck.cs'
& $runtime $compiler @args
if ($LASTEXITCODE -ne 0) { throw 'Stylesheet validator compile failed.' }
$env:MONO_PATH = Join-Path $editorData 'Managed'
& $mono $output (Join-Path $assets 'UI\onsite.uss') (Join-Path $assets 'UI\Resources\onsite.uss')
if ($LASTEXITCODE -ne 0) { throw 'Stylesheet syntax validation failed.' }
$fonts = Select-String -LiteralPath (Join-Path $assets 'UI\onsite.uss') -Pattern 'project://database/(Assets/Onsite/Fonts/[^"\)]+)' -AllMatches | ForEach-Object { $_.Matches | ForEach-Object { $_.Groups[1].Value } } | Sort-Object -Unique
$project = Split-Path -Parent (Split-Path -Parent $assets)
foreach ($font in $fonts) { if (!(Test-Path -LiteralPath (Join-Path $project $font))) { throw ('Missing font: ' + $font) } }
Write-Output ('PASS: all ' + @($fonts).Count + ' referenced font files exist.')
