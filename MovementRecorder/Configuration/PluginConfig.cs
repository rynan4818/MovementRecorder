using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace MovementRecorder.Configuration
{
    internal class PluginConfig
    {
        public static readonly string NoneCapture = "NONE";
        public static readonly string AvatarType = "Avatar";
        public static readonly string SaberType = "Saber";
        public static readonly string OtherType = "Other";
        public static PluginConfig Instance { get; set; }

        [UseConverter(typeof(ListConverter<SearchSetting>))]
        public virtual List<SearchSetting> searchSettings { get; set; } = new List<SearchSetting>()
        {
            new SearchSetting()
            {
                name = "CustomAvatar",
                type = AvatarType,
                topObjectStrings = new List<string>()
                {
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/"
                },
                rescaleString =  @"^.+/Avatar Container/SpawnedAvatar[^/]+$",
                searchStirngs = new List<string>()
                {
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+(/.+)?"
                },
                exclusionStrings = new List<string>()
                {
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/Head(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/LeftHand(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/LeftLeg(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/Pelvis(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/RightHand(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/RightLeg(/.+)?",
                    @"^.+/Avatar Container/SpawnedAvatar[^/]+/Body(/.+)?"
                }
            },
            new SearchSetting()
            {
                name = "VMCAvatar",
                type = AvatarType,
                topObjectStrings = new List<string>()
                {
                    @"^VMCAvatar/RoomAdjust/VRM/"
                },
                rescaleString =  @"^VMCAvatar/RoomAdjust/VRM$",
                searchStirngs = new List<string>()
                {
                    @"^VMCAvatar/RoomAdjust/VRM(/.+)?"
                }
            },
            new SearchSetting()
            {
                name = "NalulunaAvatars",
                type = AvatarType,
                topObjectStrings = new List<string>()
                {
                    @"^NalulunaAvatarsController/PlayerRoot/VRMAvatar/"
                },
                rescaleString =  @"^NalulunaAvatarsController/PlayerRoot/VRMAvatar$",
                searchStirngs = new List<string>()
                {
                    @"^NalulunaAvatarsController/PlayerRoot/VRMAvatar(/.+)?"
                }
            },
            new SearchSetting()
            {
                name = "SaberFactory",
                type = SaberType,
                topObjectStrings = new List<string>()
                {
                    @"^.+/VRGameCore/LeftHand/LeftSaber/SfSaberModelController[^/]+/SF Saber/",
                    @"^.+/VRGameCore/RightHand/RightSaber/SfSaberModelController[^/]+/SF Saber/"
                },
                searchStirngs = new List<string>()
                {
                    @"^.+/VRGameCore/LeftHand/LeftSaber/SfSaberModelController[^/]+/SF Saber/LeftSaber[^/]+(/.+)?",
                    @"^.+/VRGameCore/RightHand/RightSaber/SfSaberModelController[^/]+/SF Saber/RightSaber[^/]+(/.+)?"
                }
            },
            new SearchSetting()
            {
                name = "CustomSaber",
                type = SaberType,
                topObjectStrings = new List<string>()
                {
                    @"^.+/VRGameCore/LeftHand/LeftSaber/",
                    @"^.+/VRGameCore/RightHand/RightSaber/"
                },
                searchStirngs = new List<string>()
                {
                    @"^.+/VRGameCore/LeftHand/LeftSaber/LeftSaber[^/]*(/.+)?",
                    @"^.+/VRGameCore/RightHand/RightSaber/RightSaber[^/]*(/.+)?"
                }
            }
        };
        public virtual bool enabled { get; set; } = false;
        public virtual bool wipOnly { get; set; } = true;
        [NonNullable]
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> movementNames { get; set; } = new List<string>()
        {
            NoneCapture,
            NoneCapture,
            NoneCapture,
            NoneCapture,
            NoneCapture
        };
        public virtual int recordFrameRate { get; set; } = 30;
        public virtual bool movementResearch { get; set; } = false;
        public virtual float researchCheckSongSec { get; set; } = 1f;
        public virtual double oneObjectSaveTime { get; set; } = 0;
        public virtual bool notDisposeMemory { get; set; } = false;
        public virtual int minMemoryAllocation { get; set; } = 6;
        /// <summary>
        /// これは、BSIPAが設定ファイルを読み込むたびに（ファイルの変更が検出されたときを含めて）呼び出されます
        /// </summary>
        public virtual void OnReload()
        {
            // 設定ファイルを読み込んだ後の処理を行う
        }

        /// <summary>
        /// これを呼び出すと、BSIPAに設定ファイルの更新を強制します。 これは、ファイルが変更されたことをBSIPAが検出した場合にも呼び出されます。
        /// </summary>
        public virtual void Changed()
        {
            // 設定が変更されたときに何かをします
        }

        /// <summary>
        /// これを呼び出して、BSIPAに値を<paramref name ="other"/>からこの構成にコピーさせます。
        /// </summary>
        public virtual void CopyFrom(PluginConfig other)
        {
            // このインスタンスのメンバーは他から移入されました
        }
    }
    public class SearchSetting
    {
        [NonNullable]
        public virtual string name { get; set; }
        public virtual string type { get; set; }
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> topObjectStrings { get; set; }
        public virtual string rescaleString { get; set; }
        [NonNullable]
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> searchStirngs { get; set; }
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> exclusionStrings { get; set; }
    }
}
