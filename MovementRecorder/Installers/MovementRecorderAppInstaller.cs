using MovementRecorder.Models;
using Zenject;
using MovementRecorder.Playback;
using MovementRecorder.Playback.Compatibility;

namespace MovementRecorder.Installers
{
    public class MovementRecorderAppInstaller : Installer
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<RecordData>().AsSingle().NonLazy();
            this.Container.Bind<ReplaySession>().AsSingle().NonLazy();
            this.Container.Bind<ReplaySaveGuards>().AsSingle();
            this.Container.BindInterfacesAndSelfTo<Camera2ReplayInterop>().AsSingle().NonLazy();
        }
    }
}
