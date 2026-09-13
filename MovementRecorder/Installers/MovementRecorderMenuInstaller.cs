using MovementRecorder.Views;
using Zenject;
using MovementRecorder.Playback.UI;

namespace MovementRecorder.Installers
{
    public class MovementRecorderMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<ReplayMenuService>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<SettingTabViewController>().AsCached().NonLazy();
        }
    }
}
