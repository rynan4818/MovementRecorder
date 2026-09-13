param(
    [Parameter(Mandatory = $true)][string]$GameDirectory,
    [string]$DependencyDirectory,
    [string]$PluginAssembly = (Join-Path $PSScriptRoot '..\MovementRecorder\bin\Release\MovementRecorder.dll'),
    [string]$CecilAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\mono.cecil\0.11.6\lib\netstandard2.0\Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath $CecilAssembly
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($folder in @('Beat Saber_Data\Managed', 'Plugins', 'Libs')) { $resolver.AddSearchDirectory((Join-Path $GameDirectory $folder)) }
if ($DependencyDirectory) { $resolver.AddSearchDirectory([IO.Path]::GetFullPath($DependencyDirectory)) }
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$parameters.InMemory = $true
$product = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($PluginAssembly), $parameters)
$problems = [Collections.Generic.List[string]]::new()
$count = 0
$typeCount = 0
try {
    foreach ($reference in $product.MainModule.GetTypeReferences()) {
        $scope = $reference.Scope.Name
        if ($scope -match '^(mscorlib|System|netstandard|0Harmony|LiteDB|IPA.Loader)' -or $scope -eq 'MovementRecorder.dll') { continue }
        try {
            if ($null -eq $reference.Resolve()) { throw 'type not found in the compiled assembly scope' }
            $typeCount++
        } catch { $problems.Add($reference.FullName + ': ' + $_.Exception.Message) }
    }
    foreach ($reference in $product.MainModule.GetMemberReferences()) {
        $scope = $reference.DeclaringType.Scope.Name
        if ($scope -match '^(mscorlib|System|netstandard|0Harmony|LiteDB|IPA.Loader)' -or $scope -eq 'MovementRecorder.dll') { continue }
        try {
            $resolved = $reference.Resolve()
            if ($null -eq $resolved) { throw 'not found with the compiled signature' }
            $count++
        } catch { $problems.Add($reference.FullName + ': ' + $_.Exception.Message) }
    }
    if ($problems.Count -gt 0) { throw ($problems -join "`n") }
    [pscustomobject]@{ Result = 'Passed'; TypeReferences = $typeCount; MemberReferences = $count; Game = $GameDirectory; Dependencies = $DependencyDirectory; Plugin = $PluginAssembly }
} finally { $product.Dispose(); $resolver.Dispose() }
