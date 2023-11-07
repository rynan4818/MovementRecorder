using MovementRecorder.Views;
using Zenject;

namespace MovementRecorder.Installers
{
    public class MovementRecorderMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<SettingTabViewController>().AsCached().NonLazy();
        }
    }
}
