param(
    [string] $UnityData = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Data',
    [string] $Dotnet = 'C:\Program Files\dotnet\dotnet.exe',
    [string] $Node = 'C:\Program Files\nodejs\node.exe'
)
$ErrorActionPreference = 'Stop'
$netDirectory = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $PSScriptRoot '.build~'
New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$compiler = Join-Path $UnityData 'DotNetSdkRoslyn\csc.dll'
$sources = @(Get-ChildItem -LiteralPath $netDirectory -Filter '*.cs' | ForEach-Object { $_.FullName })
$generated = @('CompileCheck.dll', 'ContractTests.dll', 'ContractTests.runtimeconfig.json')

try {
    # Unity 本体は起動せず、実際の参照 DLL で各分岐をコンパイルする。
    $compileArgs = @('-nologo', '-target:library', '-nostdlib+', '-langversion:9', '-warnaserror+')
    $compileArgs += '-r:' + (Join-Path $UnityData 'NetStandard\ref\2.1.0\netstandard.dll')
    $compileArgs += Get-ChildItem (Join-Path $UnityData 'Managed\UnityEngine') -Filter '*.dll' |
        ForEach-Object { '-r:' + $_.FullName }
    foreach ($platform in @('UNITY_EDITOR', 'UNITY_WEBGL', 'UNITY_ANDROID')) {
        & $Dotnet $compiler @compileArgs @sources ('-out:' + (Join-Path $buildDirectory 'CompileCheck.dll')) ('-define:' + $platform)
        if ($LASTEXITCODE -ne 0) { throw "Compile failed: $platform" }
        Write-Output "Compiled $platform with warnings as errors"
    }

    # HTTP と PlayerPrefs を差し替え、本番への通信や端末状態の変更を避ける。
    $dotnetDirectory = Split-Path -Parent $Dotnet
    $referencePack = Get-ChildItem (Join-Path $dotnetDirectory 'packs\Microsoft.NETCore.App.Ref') -Directory |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    $referenceDirectory = Get-ChildItem (Join-Path $referencePack.FullName 'ref') -Directory | Select-Object -First 1
    $runtimeMajor = ([version]$referencePack.Name).Major
    $runtime = Get-ChildItem (Join-Path $dotnetDirectory 'shared\Microsoft.NETCore.App') -Directory |
        Where-Object { ([version]$_.Name).Major -eq $runtimeMajor } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if ($null -eq $runtime) { throw 'A matching .NET runtime is required' }
    $testArgs = @('-nologo', '-target:exe', '-nostdlib+', '-langversion:9', '-warnaserror+')
    $testArgs += Get-ChildItem $referenceDirectory.FullName -Filter '*.dll' | ForEach-Object { '-r:' + $_.FullName }
    $testArgs += Join-Path $PSScriptRoot 'UnityStubs.cs'
    $testArgs += Join-Path $PSScriptRoot 'ContractTests.cs'
    & $Dotnet $compiler @testArgs @sources ('-out:' + (Join-Path $buildDirectory 'ContractTests.dll'))
    if ($LASTEXITCODE -ne 0) { throw 'Contract test compile failed' }
    $runtimeConfig = @{ runtimeOptions = @{
        tfm = $referenceDirectory.Name
        framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name }
    }} | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $buildDirectory 'ContractTests.runtimeconfig.json'), $runtimeConfig, $utf8)
    & $Dotnet (Join-Path $buildDirectory 'ContractTests.dll')
    if ($LASTEXITCODE -ne 0) { throw 'C# contract tests failed' }
    & $Node (Join-Path $PSScriptRoot 'BridgeTests.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'WebGL bridge tests failed' }
}
finally {
    # 生成物だけを既知のパスから消す。ソースや Unity のキャッシュには触れない。
    foreach ($name in $generated) {
        $target = Join-Path $buildDirectory $name
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    if (@(Get-ChildItem -LiteralPath $buildDirectory -Force).Count -eq 0) {
        Remove-Item -LiteralPath $buildDirectory -Force
    }
}