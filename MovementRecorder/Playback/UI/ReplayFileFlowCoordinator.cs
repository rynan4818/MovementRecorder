using System;
using System.Threading.Tasks;
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
        private TaskCompletionSource<bool> _dismissal;
        public bool Opened { get; private set; }
        public bool Closing => _closing;
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
                SetTitle("MovementRecorder リプレイ"); showBackButton = true;
                _view = BeatSaberUI.CreateViewController<ReplayFileViewController>(); _view.Configure(_service);
                ProvideInitialViewControllers(_view);
            }
            _view.Refresh();
        }
        protected override void BackButtonWasPressed(ViewController topViewController) { Close(); }
        public async void Close()
        {
            if (!Opened || _closing) return;
            _service.CancelLoad();
            try { await CloseCore(); }
            catch (Exception ex) { Plugin.Log?.Warn("リプレイメニューを閉じられません: " + ex.Message); }
        }
        public Task CloseForReplay()
        {
            if (!Opened || _closing) throw new InvalidOperationException("リプレイメニューを閉じられません。");
            return CloseCore();
        }
        private Task CloseCore()
        {
            _closing = true;
            _service.MenuClosing();
            // HMUI invokes finishedCallback before TransitionDidFinish. Resume on the captured
            // Unity synchronization context after that callback has returned, never inside it.
            var dismissal = _dismissal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                BeatSaberUI.DismissFlowCoordinator(_parent, this, () =>
                {
                    Opened = false; _closing = false; _dismissal = null;
                    _service.MenuClosed(); dismissal.TrySetResult(true);
                });
            }
            catch (Exception ex) { _closing = false; _dismissal = null; dismissal.TrySetException(ex); }
            return dismissal.Task;
        }
        private void OnDestroy()
        {
            _dismissal?.TrySetCanceled();
            if (_view != null) Destroy(_view.gameObject);
        }
    }
}
