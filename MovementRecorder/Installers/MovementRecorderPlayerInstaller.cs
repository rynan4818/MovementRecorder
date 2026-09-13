using MovementRecorder.Models;
using Zenject;
using MovementRecorder.Playback;
using MovementRecorder.Playback.Runtime;

namespace MovementRecorder.Installers
{
    public class MovementRecorderPlayerInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            if (MovementReplay.IsActive)
            {
                this.Container.BindInterfacesAndSelfTo<PlaybackRuntime>().FromNewComponentOnNewGameObject().AsCached().NonLazy();
                return;
            }
            this.Container.BindInterfacesAndSelfTo<MovementRecorderController>().FromNewComponentOnNewGameObject().AsCached().NonLazy();
        }
    }
}
