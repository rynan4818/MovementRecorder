param(
    [Parameter(Mandatory = $true)][string]$GameDirectory,
    [string]$PluginAssembly = (Join-Path $PSScriptRoot '..\MovementRecorder\bin\Release\MovementRecorder.dll'),
    [string]$CecilAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\mono.cecil\0.11.6\lib\netstandard2.0\Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath $CecilAssembly
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($directory in @((Join-Path $GameDirectory 'Beat Saber_Data\Managed'), (Join-Path $GameDirectory 'Plugins'), (Join-Path $GameDirectory 'Libs'))) {
    $resolver.AddSearchDirectory($directory)
}
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$parameters.InMemory = $true
$definitions = [Collections.Generic.List[Mono.Cecil.AssemblyDefinition]]::new()
$types = @{}
$loadedAssemblies = @{}
$checks = 0
function Register-Type($type) {
    $types[$type.FullName] = $type
    foreach ($nested in $type.NestedTypes) { Register-Type $nested }
}
function Read-Assembly([string]$path) {
    $definition = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($path), $parameters)
    $definitions.Add($definition)
    $script:loadedAssemblies[$definition.Name.Name] = $true
    foreach ($type in $definition.MainModule.Types) { Register-Type $type }
    return $definition
}
function Find-Member([string]$typeName, [string]$member, [string]$kind) {
    $type = $types[$typeName]
    if ($null -eq $type) { throw "Missing type: $typeName" }
    while ($null -ne $type) {
        $members = @($type.$kind | Where-Object Name -eq $member)
        if ($members.Count -gt 0) { return $members }
        $type = if ($null -eq $type.BaseType) { $null } else { $type.BaseType.Resolve() }
    }
    throw "Missing member: $typeName.$member"
}
function Check-Field([string]$type, [string]$field, [string]$expected) {
    $members = @(Find-Member $type $field 'Fields')
    if ($members.Count -ne 1 -or ($expected -and $members[0].FieldType.FullName -ne $expected)) {
        throw "Wrong field contract: $type.$field ($expected)"
    }
    $script:checks++
}
function Check-Method([string]$type, [string]$method, [string]$returns = 'System.Void') {
    $members = @(Find-Member $type $method 'Methods')
    if ($members.Count -ne 1 -or $members[0].ReturnType.FullName -ne $returns) { throw "Wrong method contract: $type.$method" }
    $script:checks++
}
try {
    foreach ($name in @('Main', 'GameplayCore', 'HMLib', 'HMUI', 'VRUI', 'Rendering', 'HMRendering', 'UnityEngine.UI', 'UnityEngine.CoreModule', 'UnityEngine.AnimationModule')) {
        $null = Read-Assembly (Join-Path $GameDirectory "Beat Saber_Data\Managed\$name.dll")
    }
    foreach ($name in @('BSML', 'SiraUtil', 'BeatLeader', 'ScoreSaber', 'SongPlayHistoryContinued')) {
        $path = Join-Path $GameDirectory "Plugins\$name.dll"
        if (Test-Path -LiteralPath $path) { $null = Read-Assembly $path }
    }
    $product = Read-Assembly $PluginAssembly
    $fields = @{
        AudioTimeSyncController = @('_songTime', '_startSongTime', '_lastFrameDeltaSongTime', '_isReady', '_audioStartTimeOffsetSinceStart', '_audioStarted', '_audioSource')
        BeatmapCallbacksController = @('_beatmapData', '_callbacksInTimes', '_prevSongTime', '_songTime', '_startFilterTime', '_callCallbacksBehavior')
        'BeatmapCallbacksController/CallCallbacksBehaviorWithLastState' = @('_replayState')
        CallbacksInTime = @('_callbacks', '_callbacksWithSubtypeIdentifier')
        BeatmapObjectManager = @('_allBeatmapObjects')
        BeatmapObjectSpawnController = @('_beatmapObjectSpawnMovementData', '_isInitialized')
        GameSongController = @('_songDidFinish')
        Saber = @('_saberBladeTopTransform', '_saberBladeBottomTransform')
        SaberMovementData = @('_data', '_nextAddIndex', '_validCount', '_bladeSpeed', '_dataProcessors')
        SaberSwingRatingCounter = @('_cutTime')
        PauseController = @('_paused', '_wantsToPause')
        PlayerTransforms = @('_headTransform')
        NoteCutSoundEffectManager = @('_noteCutSoundEffectPoolContainer', '_prevNoteATime', '_prevNoteBTime')
        ScoreController = @('_sortedScoringElementsWithoutMultiplier', '_scoringElementsWithMultiplier', '_scoringElementsToRemove', '_sortedNoteTimesWithoutScoringElements', '_modifiedScore', '_multipliedScore', '_immediateMaxPossibleMultipliedScore', '_immediateMaxPossibleModifiedScore', '_scoreMultiplierCounter', '_maxScoreMultiplierCounter', 'scoreDidChangeEvent', 'multiplierDidChangeEvent')
        ComboController = @('_combo', '_maxCombo', 'comboDidChangeEvent')
        GameEnergyCounter = @('<energy>k__BackingField', '_batteryLives', '_didReach0Energy', '_nextFrameEnergyChange', 'gameEnergyDidChangeEvent')
        PlayerHeadAndObstacleInteraction = @('_intersectingObstacles', '_lastFrameNumCheck', '_prevFrameNumberOfIntersectingObstaclesCount')
        BeatmapObjectExecutionRatingsRecorder = @('_beatmapObjectExecutionRatings', '_hitObstacles')
        GoodCutScoringElement = @('_cutScoreBuffer')
        CutScoreBuffer = @('_saberSwingRatingCounter')
        'VRUIControls.VRPointer' = @('_leftVRController', '_rightVRController', '_vrController')
        VRController = @('_transformOffset')
        'Tweening.TweeningManager' = @('_activeTweens', '_ownerByTween')
        TrackLaneRingsRotationEffect = @('_activeRingRotationEffects')
        LightPairRotationEventEffect = @('_randomGenerationFrameNum')
        LightPairSinMoveEventEffect = @('_randomGenerationFrameNum')
        BufferedLightColorGroupEffect = @('_didReceiveEventThisFrame')
    }
    foreach ($entry in $fields.GetEnumerator()) { foreach ($field in $entry.Value) { Check-Field $entry.Key $field } }
    Check-Field 'SaberMovementData' '_data' 'BladeMovementDataElement[]'
    Check-Field 'SaberSwingRatingCounter' '_cutTime' 'System.Single'
    Check-Method 'MainCamera' 'get_camera' 'UnityEngine.Camera'
    Check-Method 'SiraUtil.Tools.FPFC.IFPFCSettings' 'get_Enabled' 'System.Boolean'
    Check-Method 'Saber' 'OverridePositionAndRotation'
    Check-Field 'VRUIControls.VRPointer' '_laserPointerPrefab' 'VRUIControls.VRLaserPointer'
    Check-Field 'VRUIControls.VRPointer' '_cursorPrefab' 'UnityEngine.Transform'
    Check-Field 'VRUIControls.VRPointer' '_defaultLaserPointerLength' 'System.Single'
    Check-Field 'VRUIControls.VRPointer' '_laserPointerWidth' 'System.Single'
    Check-Field 'VRUIControls.VRInputModule' '_vrPointer' 'VRUIControls.VRPointer'
    Check-Field 'VRUIControls.VRInputModule' '_rumblePreset' 'Libraries.HM.HMLib.VR.HapticPresetSO'
    Check-Field 'VRUIControls.VRInputModule' '_hapticFeedbackController' 'HapticFeedbackController'
    Check-Method 'VRUIControls.VRInputModule' 'ClearSelection'
    Check-Method 'VRUIControls.VRInputModule' 'set_useMouseForPressInput'
    Check-Method 'VRUIControls.VRPointer' 'DestroyLaserAndHit'
    Check-Method 'VRController' 'Update'
    Check-Method 'UnityEngine.EventSystems.EventSystem' 'get_current' 'UnityEngine.EventSystems.EventSystem'
    Check-Method 'UnityEngine.EventSystems.EventSystem' 'set_current'
    Check-Method 'UnityEngine.EventSystems.EventSystem' 'UpdateModules'
    Check-Method 'UnityEngine.Animator' 'get_cullingMode' 'UnityEngine.AnimatorCullingMode'
    Check-Field 'BloomPrePass' '_bloomPrePassRenderData' 'BloomPrePassRenderDataSO'
    Check-Field 'BloomPrePass' '_renderData' 'BloomPrePassRenderDataSO/Data'
    Check-Field 'MainEffectController' '_imageEffectController' 'ImageEffectController'
    Check-Field 'MainEffectController' 'afterImageEffectEvent' 'System.Action`1<UnityEngine.RenderTexture>'
    Check-Method 'BloomPrePass' 'SetMode'
    Check-Method 'MainEffectController' 'OnEnable'
    Check-Method 'CameraDepthTextureMode' 'Awake'
    foreach ($field in @('m_Type0', 'm_Type1', 'm_Type2')) { Check-Field 'UnityEngine.RequireComponent' $field 'System.Type' }
    Check-Method 'UnityEngine.GameObject' 'get_layer' 'System.Int32'
    Check-Method 'UnityEngine.GameObject' 'set_layer'
    Check-Method 'UnityEngine.Camera' 'get_cullingMask' 'System.Int32'
    Check-Method 'UnityEngine.Camera' 'set_cullingMask'
    $cameraClone = $product.MainModule.GetType('MovementRecorder.Playback.Runtime.SpectatorCameraClone')
    if ($null -eq $cameraClone) { throw 'Missing spectator camera clone helper' }
    $cloneCalls = @($cameraClone.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object {
        $_.Operand -is [Mono.Cecil.GenericInstanceMethod] -and $_.Operand.DeclaringType.FullName -eq 'UnityEngine.Object' -and $_.Operand.Name -eq 'Instantiate'
    })
    if ($cloneCalls.Count -ne 1 -or $cloneCalls[0].Operand.GenericArguments[0].FullName -ne 'UnityEngine.Camera' -or
        $cloneCalls[0].Operand.Parameters.Count -ne 3 -or
        $cloneCalls[0].Operand.Parameters[0].ParameterType -isnot [Mono.Cecil.GenericParameter] -or
        $cloneCalls[0].Operand.Parameters[0].ParameterType.Position -ne 0 -or
        $cloneCalls[0].Operand.Parameters[1].ParameterType.FullName -ne 'UnityEngine.Transform' -or
        $cloneCalls[0].Operand.Parameters[2].ParameterType.FullName -ne 'System.Boolean') {
        throw 'Spectator camera must be instantiated with its inactive parent supplied at creation'
    }
    $null = $cloneCalls[0].Operand.Resolve()
    $checks++
    Check-Method 'UnityEngine.Animator' 'set_cullingMode'
    Check-Method 'UnityEngine.SkinnedMeshRenderer' 'get_sharedMesh' 'UnityEngine.Mesh'
    Check-Method 'UnityEngine.SkinnedMeshRenderer' 'GetBlendShapeWeight' 'System.Single'
    Check-Method 'UnityEngine.SkinnedMeshRenderer' 'SetBlendShapeWeight'
    Check-Method 'UnityEngine.Mesh' 'get_blendShapeCount' 'System.Int32'
    $blendShapeSetter = Find-Member 'UnityEngine.SkinnedMeshRenderer' 'SetBlendShapeWeight' 'Methods'
    if (($blendShapeSetter.Parameters.ParameterType.FullName -join ',') -ne 'System.Int32,System.Single') { throw 'Wrong BlendShape setter signature' }
    $cullingSetter = Find-Member 'UnityEngine.Animator' 'set_cullingMode' 'Methods'
    if (($cullingSetter.Parameters.ParameterType.FullName -join ',') -ne 'UnityEngine.AnimatorCullingMode') { throw 'Wrong Animator culling setter signature' }
    $checks += 2
    Check-Field 'PlayerHeadAndObstacleInteraction' '_intersectingObstacles' 'System.Collections.Generic.HashSet`1<ObstacleController>'
    Check-Field 'GameEnergyCounter' '_batteryLives' 'System.Int32'
    $hooks = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\MovementRecorder\Playback\Runtime\ReplayRuntimeHooks.cs') -Raw
    foreach ($match in [regex]::Matches($hooks, 'Hook\(typeof\(([\w.]+)\),\s*"([^"]+)"')) { Check-Method $match.Groups[1].Value $match.Groups[2].Value }
    foreach ($type in @('ScoreController', 'ComboController', 'GameEnergyCounter')) {
        Check-Method $type 'HandleNoteWasCut'; Check-Method $type 'HandleNoteWasMissed'
    }
    Check-Method 'Tweening.TweeningManager' 'LateUpdate'
    Check-Method 'Tweening.TweeningManager' 'AddTweenToDataStructures' 'System.Boolean'
    foreach ($type in @('LightRotationEventEffect', 'LightPairRotationEventEffect', 'LightPairSinMoveEventEffect')) { Check-Method $type 'Update' }
    foreach ($contract in @(
        @('BeatLeader', 'BeatLeader.Installers.OnGameplayCoreInstaller', 'InitRecorder'),
        @('BeatLeader', 'BeatLeader.Utils.ScoreUtil', 'ProcessReplay'),
        @('ScoreSaber', 'ScoreSaber.Features.Replays.Installers.RecordInstaller', 'InstallBindings'),
        @('ScoreSaber', 'ScoreSaber.Features.ScoreSubmission.ScoreSubmissionController', 'HandleStandardLevelFinished'),
        @('SongPlayHistoryContinued', 'SongPlayHistoryContinued.Plugin', 'SaveRecord')
    )) { if ($loadedAssemblies.ContainsKey($contract[0])) { Check-Method $contract[1] $contract[2] } }
    # HDT distance recording is allowed. Neither HDT nor HDT Counter may be a patch target,
    # and their private APIs must not be required for replay startup or contract validation.
    $compatibilityTypes = @($product.MainModule.Types | Where-Object Namespace -eq 'MovementRecorder.Playback.Compatibility')
    $hdtHooks = @($compatibilityTypes | ForEach-Object { $_.Methods } | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object {
        ($_.OpCode.Name -eq 'ldstr' -and $_.Operand -match 'HeadDistanceTravelled|HDTCounter') -or
        ($_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -match 'HeadDistanceTravelled|HDTCounter|HeadDistanceReplayGuard')
    })
    if ($hdtHooks.Count -ne 0 -or $null -ne $product.MainModule.GetType('MovementRecorder.Playback.Compatibility.HeadDistanceReplayGuard')) {
        throw 'Replay must not hook HDT or HDT Counter: distance measurement and storage must remain untouched'
    }
    $checks++
    # A model provider can derive from SaberTrail without ever setting native movementData.
    # Replay must drive the live Saber and leave every provider's trail lifecycle alone.
    $driver = $product.MainModule.GetType('MovementRecorder.Playback.Runtime.RecordedSaberDriver')
    $trailCalls = @($driver.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -match 'SaberTrail|TrailRenderer|SaberFactory'
    })
    if ($trailCalls.Count -ne 0) { throw 'RecordedSaberDriver must not call native or provider trail lifecycle methods' }
    $checks++
    # Only the original provider evaluates expressions. The clone must contain renderer data only.
    $modelTypes = @($types.Values | Where-Object FullName -like 'MovementRecorder.Playback.Models.RenderModelClone*')
    foreach ($instruction in $modelTypes.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions }) {
        $call = $instruction.Operand
        if ($call -isnot [Mono.Cecil.MethodReference]) { continue }
        if ($call.DeclaringType.FullName -eq 'UnityEngine.Object' -and $call.Name -eq 'Instantiate') { throw 'RenderModelClone must not instantiate provider scripts' }
        if ($call.Name -eq 'AddComponent') {
            if ($call -isnot [Mono.Cecil.GenericInstanceMethod] -or $call.GenericArguments.Count -ne 1 -or
                $call.GenericArguments[0].FullName -notin @('UnityEngine.SkinnedMeshRenderer', 'UnityEngine.MeshRenderer', 'UnityEngine.MeshFilter', 'UnityEngine.LODGroup')) {
                throw "RenderModelClone must not copy animation or provider components: $call"
            }
        }
        if ($call.DeclaringType.FullName -eq 'UnityEngine.Animator' -and $call.Name -notin @('get_cullingMode', 'set_cullingMode')) {
            throw "RenderModelClone must not drive the original Animator state: $call"
        }
    }
    $checks++
    $resources = @{}
    foreach ($resource in $product.MainModule.Resources) { $resources[$resource.Name] = $resource }
    foreach ($name in @('LiteDB', 'System.Buffers')) {
        $stream = $resources["MovementRecorder.Dependencies.$name.dll"].GetResourceStream()
        try {
            $embedded = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($stream)
            try {
                if ($embedded.Name.Name -ne $name) { throw "Wrong embedded dependency: $name" }
                if ($name -eq 'LiteDB' -and $embedded.Name.Version.ToString() -ne '5.0.21.0') { throw 'Wrong LiteDB version' }
            } finally { $embedded.Dispose() }
        } finally { $stream.Dispose() }
        $checks++
    }
    foreach ($reference in $product.MainModule.AssemblyReferences) {
        if ($reference.Name -in @('BeatLeader', 'ScoreSaber', 'HeadDistanceTravelled', 'HDTCounter', 'CustomAvatar', 'VMCAvatar', 'NalulunaAvatars', 'NalulunaAvatarsLite', 'CustomKeyEvents', 'VRM', 'VRM10', 'UniGLTF', 'SaberFactory', 'CustomSaber', 'CustomSabers')) { throw "Unexpected assembly dependency: $($reference.Name)" }
    }
    $checks++
    $bindings = @{}
    foreach ($type in $product.MainModule.Types) {
        foreach ($member in @($type.Fields) + @($type.Properties) + @($type.Methods)) {
            foreach ($attribute in $member.CustomAttributes) {
                if ($attribute.AttributeType.Name -in @('UIValue', 'UIValueAttribute', 'UIAction', 'UIActionAttribute')) {
                    $bindings[$attribute.ConstructorArguments[0].Value] = $true
                }
            }
        }
    }
    foreach ($resource in $product.MainModule.Resources | Where-Object Name -like '*.bsml') {
        $reader = [IO.StreamReader]::new($resource.GetResourceStream())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        foreach ($attribute in $xml.SelectNodes('//@*')) {
            $binding = if ($attribute.Value.StartsWith('~')) { $attribute.Value.Substring(1) } elseif ($attribute.Name -in @('on-click', 'value', 'choices', 'contents', 'select-cell', 'formatter')) { $attribute.Value } else { $null }
            if ($binding -and !$bindings.ContainsKey($binding)) { throw "Unbound BSML attribute: $($resource.Name) $($attribute.Name)=$binding" }
            if ($binding) { $checks++ }
        }
        # A binding name alone cannot prove that BSML can inject the component or invoke an action.
        # BSML 1.6.10's custom-list creates CustomCellListTableData and passes the row object on selection.
        $uiProblems = [Collections.Generic.List[string]]::new()
        foreach ($list in $xml.SelectNodes('//custom-list')) {
            $hostType = $product.MainModule.GetType($resource.Name.Substring(0, $resource.Name.Length - 5))
            if ($null -eq $hostType) { throw "Missing view host: $($resource.Name)" }
            $component = @($hostType.Fields | Where-Object {
                @($_.CustomAttributes | Where-Object { $_.AttributeType.Name -in @('UIComponent', 'UIComponentAttribute') -and $_.ConstructorArguments[0].Value -eq $list.GetAttribute('id') }).Count -gt 0
            })
            if ($component.Count -ne 1 -or $component[0].FieldType.FullName -ne 'BeatSaberMarkupLanguage.Components.CustomCellListTableData') {
                $uiProblems.Add("custom-list '$($list.GetAttribute('id'))' must bind CustomCellListTableData")
            } else { $checks++ }
            $action = @($hostType.Methods | Where-Object {
                @($_.CustomAttributes | Where-Object { $_.AttributeType.Name -in @('UIAction', 'UIActionAttribute') -and $_.ConstructorArguments[0].Value -eq $list.GetAttribute('select-cell') }).Count -gt 0
            })
            if ($action.Count -ne 1 -or $action[0].Parameters.Count -ne 2 -or $action[0].Parameters[0].ParameterType.FullName -ne 'HMUI.TableView' -or
                $action[0].Parameters[1].ParameterType.FullName -notin @('System.Object', 'MovementRecorder.Playback.UI.ReplayFileRow')) {
                $uiProblems.Add("custom-list '$($list.GetAttribute('id'))' selection must accept (TableView, row object), not an integer index")
            } else { $checks++ }
            Check-Field 'BeatSaberMarkupLanguage.Components.CustomCellListTableData' 'tableView' 'HMUI.TableView'
            Check-Field 'BeatSaberMarkupLanguage.Components.CustomCellListTableData' 'data' 'System.Collections.Generic.List`1<System.Object>'
        }
        if ($uiProblems.Count -gt 0) { throw ($uiProblems -join '; ') }
    }
    $reader = [IO.StreamReader]::new($resources['MovementRecorder.manifest.json'].GetResourceStream())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($manifest.gameVersion -ne '1.20.0') { throw 'gameVersion was changed' }
    $checks++
    [pscustomobject]@{ Checks = $checks; Result = 'Passed'; Plugin = [IO.Path]::GetFullPath($PluginAssembly); GameVersionInManifest = $manifest.gameVersion }
} finally {
    foreach ($definition in $definitions) { $definition.Dispose() }
    $resolver.Dispose()
}
