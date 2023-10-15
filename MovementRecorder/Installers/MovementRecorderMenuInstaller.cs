using MovementRecorder.Views;
using Zenject;

namespace MovementRecorder.Installers
{
    public class MovementRecorderMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<SettingTabViewController>().FromNewComponentAsViewController().AsSingle().NonLazy();
            //this.Container.BindInterfacesAndSelfTo<ConfigViewController>().FromNewComponentAsViewController().AsSingle().NonLazy();
        }
    }
}
