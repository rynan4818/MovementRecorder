using BeatSaberMarkupLanguage;
using HMUI;
using UnityEngine;

namespace MovementRecorder.Playback.UI
{
    internal sealed class ReplayFileFlowCoordinator : FlowCoordinator
    {
        private ReplayMenuService _service;
        private ReplayFileViewController _view;
        private FlowCoordinator _parent;
        private bool _closing;
        public bool Opened { get; private set; }
        public void Configure(ReplayMenuService service) { _service = service; }
        public void Show()
        {
            if (Opened) return;
            _parent = BeatSaberUI.MainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf(); Opened = true;
            BeatSaberUI.PresentFlowCoordinator(_parent, this);
        }
        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("MovementRecorder 記録ファイル"); showBackButton = true;
                _view = BeatSaberUI.CreateViewController<ReplayFileViewController>(); _view.Configure(_service);
                ProvideInitialViewControllers(_view);
            }
            _view.Refresh();
        }
        protected override void BackButtonWasPressed(ViewController topViewController) { Close(); }
        public void Close()
        {
            if (!Opened || _closing) return;
            _closing = true;
            BeatSaberUI.DismissFlowCoordinator(_parent, this, () => { Opened = false; _closing = false; _service.PickerClosed(); });
        }
        private void OnDestroy() { if (_view != null) Destroy(_view.gameObject); }
    }
}
