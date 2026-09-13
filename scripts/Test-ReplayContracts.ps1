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
    foreach ($name in @('Main', 'GameplayCore', 'HMLib', 'HMUI', 'VRUI', 'Rendering', 'HMRendering', 'UnityEngine.UI', 'Unity.TextMeshPro', 'UnityEngine.CoreModule', 'UnityEngine.AnimationModule', 'IPA.Loader')) {
        $null = Read-Assembly (Join-Path $GameDirectory "Beat Saber_Data\Managed\$name.dll")
    }
    foreach ($name in @('BSML', 'SiraUtil', 'BeatLeader', 'ScoreSaber', 'SongPlayHistoryContinued', 'Camera2')) {
        $path = Join-Path $GameDirectory "Plugins\$name.dll"
        if (Test-Path -LiteralPath $path) { $null = Read-Assembly $path }
    }
    $product = Read-Assembly $PluginAssembly
    Check-Method 'IPA.Loader.PluginManager' 'GetPluginFromId' 'IPA.Loader.PluginMetadata'
    Check-Method 'IPA.Loader.PluginMetadata' 'get_Assembly' 'System.Reflection.Assembly'
    if ($loadedAssemblies.ContainsKey('Camera2')) {
        foreach ($contract in @(
            @('Camera2.SDK.ReplaySources', 'Register', 'Camera2.SDK.ReplaySources/ISource', $true),
            @('Camera2.SDK.ReplaySources', 'Unregister', 'Camera2.SDK.ReplaySources/ISource', $true),
            @('Camera2.SDK.ReplaySources/GenericSource', '.ctor', 'System.String', $false),
            @('Camera2.SDK.ReplaySources/GenericSource', 'SetActive', 'System.Boolean', $false),
            @('Camera2.SDK.ReplaySources/GenericSource', 'Update', 'UnityEngine.Vector3&,UnityEngine.Quaternion&', $false)
        )) {
            Check-Method $contract[0] $contract[1]
            $method = Find-Member $contract[0] $contract[1] 'Methods'
            if (!$method.IsPublic -or $method.IsStatic -ne $contract[3] -or ($method.Parameters.ParameterType.FullName -join ',') -ne $contract[2]) {
                throw "Wrong Camera2 public API contract: $($contract[0]).$($contract[1])"
            }
            $checks++
        }
    }
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
    Check-Method 'HMUI.TableView' 'ClearSelection'
    Check-Method 'HMUI.TableView' 'SelectCellWithIdx'
    $selectCell = Find-Member 'HMUI.TableView' 'SelectCellWithIdx' 'Methods'
    if (($selectCell.Parameters.ParameterType.FullName -join ',') -ne 'System.Int32,System.Boolean') { throw 'Wrong table selection signature' }
    $dismiss = Find-Member 'BeatSaberMarkupLanguage.BeatSaberUI' 'DismissFlowCoordinator' 'Methods'
    if (($dismiss.Parameters.ParameterType.FullName -join ',') -ne 'HMUI.FlowCoordinator,HMUI.FlowCoordinator,System.Action,HMUI.ViewController/AnimationDirection,System.Boolean') {
        throw 'Wrong BSML flow dismissal callback signature'
    }
    $checks += 2
    $textHandler = $types['BeatSaberMarkupLanguage.TypeHandlers.TextMeshProUGUIHandler']
    $textProps = @($textHandler.Methods | Where-Object Name -eq 'get_Props' | ForEach-Object { $_.Body.Instructions } |
        Where-Object { $_.OpCode.Name -eq 'ldstr' } | ForEach-Object Operand)
    foreach ($property in @('font-color', 'rich-text')) {
        if ($property -notin $textProps) { throw "Missing BSML text property: $property" }
        $checks++
    }
    Check-Method 'BeatSaberMarkupLanguage.Components.CustomCellTableCell' 'RefreshVisuals'
    Check-Method 'TMPro.TMP_Text' 'set_text'
    Check-Method 'UnityEngine.UI.Graphic' 'set_color'
    $rowType = $product.MainModule.GetType('MovementRecorder.Playback.UI.ReplayFileRow')
    foreach ($id in @('row-info', 'row-distance')) {
        $textField = @($rowType.Fields | Where-Object {
            @($_.CustomAttributes | Where-Object { $_.AttributeType.Name -in @('UIComponent', 'UIComponentAttribute') -and $_.ConstructorArguments[0].Value -eq $id }).Count -eq 1
        })
        if ($textField.Count -ne 1 -or $textField[0].FieldType.FullName -ne 'TMPro.TextMeshProUGUI') { throw "Wrong replay row component: $id" }
        $checks++
    }
    $rowRefresh = @($rowType.Methods | Where-Object {
        @($_.CustomAttributes | Where-Object { $_.AttributeType.Name -in @('UIAction', 'UIActionAttribute') -and $_.ConstructorArguments[0].Value -eq 'refresh-visuals' }).Count -eq 1
    })
    if ($rowRefresh.Count -ne 1 -or ($rowRefresh[0].Parameters.ParameterType.FullName -join ',') -ne 'System.Boolean,System.Boolean') {
        throw 'Replay cell refresh must accept the BSML selected/highlighted flags'
    }
    $checks++
    $config = $product.MainModule.GetType('MovementRecorder.Configuration.PluginConfig').Properties | Where-Object Name -eq 'showReplaySourceAvatar'
    if ($null -eq $config -or $config.PropertyType.FullName -ne 'System.Boolean' -or !$config.GetMethod.IsVirtual -or !$config.SetMethod.IsVirtual) {
        throw 'Source avatar setting must be a persisted virtual bool property'
    }
    $checks++
    foreach ($setting in @(
        @('offsetReplaySourceAvatarWithHmd', 'System.Boolean'),
        @('replayObserverX', 'System.Single'), @('replayObserverY', 'System.Single'), @('replayObserverZ', 'System.Single')
    )) {
        $property = $product.MainModule.GetType('MovementRecorder.Configuration.PluginConfig').Properties | Where-Object Name -eq $setting[0]
        if ($null -eq $property -or $property.PropertyType.FullName -ne $setting[1] -or
            !$property.GetMethod.IsPublic -or !$property.SetMethod.IsPublic -or !$property.GetMethod.IsVirtual -or !$property.SetMethod.IsVirtual) {
            throw "Observer setting must be a public persisted virtual property: $($setting[0])"
        }
        $checks++
    }
    foreach ($callback in @('onPreCull', 'onPostRender')) { Check-Field 'UnityEngine.Camera' $callback 'UnityEngine.Camera/CameraCallback' }
    Check-Method 'UnityEngine.Camera/CameraCallback' 'Invoke'
    $cameraCallback = Find-Member 'UnityEngine.Camera/CameraCallback' 'Invoke' 'Methods'
    if (($cameraCallback.Parameters.ParameterType.FullName -join ',') -ne 'UnityEngine.Camera') { throw 'Wrong camera render callback signature' }
    $checks++
    Check-Method 'UnityEngine.SkinnedMeshRenderer' 'get_updateWhenOffscreen' 'System.Boolean'
    Check-Method 'UnityEngine.SkinnedMeshRenderer' 'set_updateWhenOffscreen'
    Check-Method 'UnityEngine.WaitForEndOfFrame' '.ctor'
    Check-Method 'UnityEngine.Time' 'get_frameCount' 'System.Int32'
    $guard = $product.MainModule.GetType('MovementRecorder.Playback.Models.SourceAvatarOffsetFrameGuard')
    $order = @($guard.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'UnityEngine.DefaultExecutionOrder' })
    if ($order.Count -ne 1 -or $order[0].ConstructorArguments[0].Value -ne -32000) { throw 'Avatar restoration must run before provider simulation' }
    $checks++
    $runtime = $product.MainModule.GetType('MovementRecorder.Playback.Runtime.PlaybackRuntime')
    foreach ($method in $runtime.Methods | Where-Object Name -in @('Update', 'LateUpdate', 'WriteLatePoses')) {
        if (@($method.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('UpdateSourceAvatarOffset', 'SetSourceAvatarOffset')
        }).Count -ne 0) { throw 'Avatar offset setup must not run from the regular replay frame loop' }
    }
    $checks++
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
        @('SongPlayHistoryContinued', 'SongPlayHistoryContinued.Plugin', 'SaveRecord')
    )) { if ($loadedAssemblies.ContainsKey($contract[0])) { Check-Method $contract[1] $contract[2] } }
    if ($loadedAssemblies.ContainsKey('ScoreSaber')) {
        if ($types.ContainsKey('ScoreSaber.Features.Replays.ReplayStateRegistry')) {
            Check-Method 'ScoreSaber.Features.Replays.Installers.RecordInstaller' 'InstallBindings'
            Check-Method 'ScoreSaber.Features.ScoreSubmission.ScoreSubmissionController' 'HandleStandardLevelFinished'
            Check-Method 'ScoreSaber.Features.Replays.ReplayStateRegistry' 'get_IsPlaybackEnabled' 'System.Boolean'
        } else {
            Check-Method 'ScoreSaber.Core.ReplaySystem.Installers.RecordInstaller' 'InstallBindings'
            Check-Method 'ScoreSaber.Core.Daemons.UploadDaemon' 'Three'
            Check-Method 'ScoreSaber.Plugin' 'get_Instance' 'ScoreSaber.Plugin'
            Check-Method 'ScoreSaber.Plugin' 'get_ReplayState' 'ScoreSaber.Core.ReplaySystem.ReplayState'
            Check-Field 'ScoreSaber.Core.ReplaySystem.ReplayState' 'IsPlaybackEnabled' 'System.Boolean'
        }
    }
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
        if ($reference.Name -in @('Camera2', 'CameraPlus', 'BeatLeader', 'ScoreSaber', 'HeadDistanceTravelled', 'HDTCounter', 'CustomAvatar', 'VMCAvatar', 'NalulunaAvatars', 'NalulunaAvatarsLite', 'CustomKeyEvents', 'VRM', 'VRM10', 'UniGLTF', 'SaberFactory', 'CustomSaber', 'CustomSabers')) { throw "Unexpected assembly dependency: $($reference.Name)" }
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
        if ($resource.Name -in @('MovementRecorder.Playback.UI.ReplayFileViewController.bsml', 'MovementRecorder.Playback.UI.ReplayControlsViewController.bsml')) {
            foreach ($axis in @(@('observer-x', '-5', '5'), @('observer-y', '-3', '3'), @('observer-z', '-8', '5'))) {
                $setting = @($xml.SelectNodes("//increment-setting[@value='$($axis[0])']"))
                if ($setting.Count -ne 1 -or $setting[0].GetAttribute('increment') -ne '0.1' -or
                    $setting[0].GetAttribute('min') -ne $axis[1] -or $setting[0].GetAttribute('max') -ne $axis[2]) {
                    throw "Wrong 0.1 m observer control: $($resource.Name) $($axis[0])"
                }
                $checks++
            }
        }
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
    if ($manifest.gameVersion -ne '1.29.0') { throw 'gameVersion was changed' }
    $checks++
    if ($manifest.version -ne '0.3.0') { throw 'Plugin version was changed' }
    if ($manifest.dependsOn.PSObject.Properties.Name -contains 'Camera2' -or $manifest.dependsOn.PSObject.Properties.Name -contains 'CameraPlus') {
        throw 'Camera MODs must remain optional'
    }
    if ($manifest.loadAfter -notcontains 'Camera2') { throw 'Missing optional Camera2 load order' }
    $checks += 3
    [pscustomobject]@{ Checks = $checks; Result = 'Passed'; Plugin = [IO.Path]::GetFullPath($PluginAssembly); GameVersionInManifest = $manifest.gameVersion }
} finally {
    foreach ($definition in $definitions) { $definition.Dispose() }
    $resolver.Dispose()
}
