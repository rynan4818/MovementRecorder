using MovementRecorder.Installers;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using HarmonyLib;
using IPALogger = IPA.Logging.Logger;
using System.Reflection;

namespace MovementRecorder
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
        public const string HARMONY_ID = "com.github.rynan4818.MovementRecorder";
        private static Harmony _harmony;
        [Init]
        /// <summary>
        /// IPAによってプラグインが最初にロードされたときに呼び出される（ゲームが開始されたとき、またはプラグインが無効な状態で開始された場合は有効化されたときのいずれか）
        /// [Init]コンストラクタを使用するメソッドや、InitWithConfigなどの通常のメソッドの前に呼び出されるメソッド
        /// [Init]は1つのコンストラクタにのみ使用してください
        /// </summary>
        public void Init(IPALogger logger, Config conf, Zenjector zenjector)
        {
            Instance = this;
            Log = logger;
            Log.Info("MovementRecorder initialized.");

            //BSIPAのConfigを使用する場合はコメントを外します
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Log.Debug("Config loaded");

            //使用するZenjectのインストーラーのコメントを外します
            zenjector.Install<MovementRecorderAppInstaller>(Location.App);
            //zenjector.Install<MovementRecorderMenuInstaller>(Location.Menu);
            zenjector.Install<MovementRecorderPlayerInstaller>(Location.Player);
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Log.Debug("OnApplicationStart");
            _harmony = new Harmony(HARMONY_ID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());        }

        [OnExit]
        public void OnApplicationQuit()
        {
            Log.Debug("OnApplicationQuit");
            _harmony.UnpatchSelf();
        }
    }
}
