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
        public static PluginConfig Instance { get; set; }

        [UseConverter(typeof(ListConverter<SearchSetting>))]
        public virtual List<SearchSetting> searchSettings { get; set; } = new List<SearchSetting>()
        {
            new SearchSetting()
            {
                name = "CustomAvatar",
                topObjectString = @"^.+/VRGameCore/Avatar Container/SpawnedAvatar[^/]+/",
                rescaleStrings = new List<string>()
                {
                    @"^.+/VRGameCore/Avatar Container/SpawnedAvatar[^/]+/"
                },
                searchStirngs = new List<string>()
                {
                    @"^.+/VRGameCore/Avatar Container/SpawnedAvatar[^/]+/",
                    @"^.+/VRGameCore/Avatar Container/SpawnedAvatar[^/]+/[^/]+/Armature(/.+)?"
                }
            },
            new SearchSetting()
            {
                name = "SaberFactory",
                topObjectString = @"^.+/VRGameCore/(Left|Right)Hand/(Left|Right)Saber/SfSaberModelController[^/]+/SF Saber/",
                searchStirngs = new List<string>()
                {
                    @"^.+/VRGameCore/LeftHand/LeftSaber/SfSaberModelController[^/]+/SF Saber/LeftSaber[^/]+(/.+)?",
                    @"^.+/VRGameCore/RightHand/RightSaber/SfSaberModelController[^/]+/SF Saber/RightSaber[^/]+(/.+)?"
                }
            }
        };
        public virtual bool enabled { get; set; } = false;
        public virtual bool wipOnly { get; set; } = true;
        [NonNullable]
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> motionCaptures { get; set; } = new List<string>()
        {
            "CustomAvatar",
            "SaberFactory",
            NoneCapture,
            NoneCapture,
            NoneCapture
        };
        public virtual int recordFrameRate { get; set; } = 30;
        public virtual bool motionResearch { get; set; } = false;
        public virtual bool researchWorldSpace { get; set; } = false;
        public virtual float researchCheckSongSec { get; set; } = 1f;
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
        public virtual string topObjectString { get; set; }
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> rescaleStrings { get; set; }
        [NonNullable]
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> searchStirngs { get; set; }
        [UseConverter(typeof(ListConverter<string>))]
        public virtual List<string> exclusionStrings { get; set; }
    }
}
