# Third-party notices

MovementRecorder's existing license and attribution remain applicable to this project.

This replay build embeds the following independently licensed libraries. Their DLLs are resolved only when required by MovementRecorder's optional distance reader; they are not installed over another MOD's dependencies.

| Library | Version | License text |
| --- | --- | --- |
| LiteDB | 5.0.21 | `licenses/LiteDB-LICENSE.txt` (MIT, Mauricio David) |
| System.Buffers | 4.5.1 | `licenses/System.Buffers-LICENSE.txt` (MIT, .NET Foundation and Contributors) |

The upstream System.Buffers third-party notices are also included in `licenses/System.Buffers-THIRD-PARTY-NOTICES.txt`.

The build uses BSIPA.AssemblyPublicizer.MSBuild 0.5.0 to create local reference assemblies. Its generated `IgnoresAccessChecksToAttribute` is compiled into MovementRecorder; the build tool and game reference DLLs are not shipped. See `licenses/BSIPA.AssemblyPublicizer-LICENSE.txt` (MIT, BepInEx).

The replay design was informed by the local BeatLeader, ScoreSaber, SaberFactory, ChroMapper-CameraMovement, HeadDistanceTravelled and Beat Saber 1.29.1 source/API investigations documented in the approved design and fix reports. No source file from these projects, avatar MOD implementation, game DLL, mesh, texture or recording is included in the package. Runtime integration uses the user's installed game and MOD APIs. The normal required dependencies remain BSIPA, SiraUtil, BSML and SongCore.
