using System;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using UnityEngine;

namespace MovementRecorder.Playback.Runtime
{
    internal sealed class RecordedSaberDriver : IDisposable
    {
        private sealed class Hand
        {
            public Saber Saber;
            public Transform Anchor, Top, Bottom;
            public int Track;
            public Vector3 AnchorPosition;
            public Quaternion AnchorRotation;
        }
        private readonly MovementClip _clip;
        private readonly TimeHelper _time;
        private readonly Hand[] _hands;
        private readonly Dictionary<VRController, bool> _trackingStates = new Dictionary<VRController, bool>();
        private float? _pausedAt;
        public Transform[] SaberRoots => _hands.Select(h => h.Saber.transform).ToArray();
        public RecordedSaberDriver(MovementClip clip, ModelBindingPlan plan, SaberManager manager, BindingProfile profile, TimeHelper time)
        {
            _clip = clip;
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _hands = new[] { Bind(manager.leftSaber, profile.LeftAnchor, plan), Bind(manager.rightSaber, profile.RightAnchor, plan) };
            if (_hands[0].Track == _hands[1].Track) throw new InvalidOperationException("The left and right sabers need separate recorded tracks.");
            foreach (var hand in _hands)
            {
                GameAccess.Field(typeof(SaberMovementData), "_data"); GameAccess.Field(typeof(SaberMovementData), "_nextAddIndex");
                GameAccess.Field(typeof(SaberMovementData), "_validCount"); GameAccess.Field(typeof(SaberMovementData), "_bladeSpeed");
                GameAccess.Field(typeof(SaberMovementData), "_dataProcessors"); GameAccess.Field(typeof(SaberSwingRatingCounter), "_cutTime");
                if (!_clip.TryEvaluate(hand.Track, clip.StartTime, out _, out bool active) || !active)
                    throw new InvalidOperationException("The recording has no initial saber pose.");
            }
        }
        private Hand Bind(Saber saber, string explicitAnchor, ModelBindingPlan plan)
        {
            if (saber == null) throw new InvalidOperationException("Select a two-saber Standard map.");
            var candidates = Enumerable.Range(0, plan.Sources.Length).Where(i => plan.Types[i] == "Saber" && plan.Sources[i] != null &&
                (plan.Sources[i] == saber.transform || plan.Sources[i].IsChildOf(saber.transform)));
            if (!string.IsNullOrEmpty(explicitAnchor)) candidates = candidates.Where(i => _clip.Header.objectNames[i] == explicitAnchor);
            int[] roots = candidates.Where(i => !candidates.Any(j => i != j && plan.Sources[i].IsChildOf(plan.Sources[j]))).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("Cannot identify a unique fixed root for " + saber.saberType + ". Select a saber anchor in Model Mapping.");
            Transform anchor = plan.Sources[roots[0]];
            Quaternion inverse = Quaternion.Inverse(saber.transform.rotation);
            return new Hand { Saber = saber, Anchor = anchor, Track = roots[0],
                AnchorPosition = inverse * (anchor.position - saber.transform.position), AnchorRotation = inverse * anchor.rotation,
                Top = GameAccess.Get<Transform>(saber, "_saberBladeTopTransform"), Bottom = GameAccess.Get<Transform>(saber, "_saberBladeBottomTransform") };
        }
        public void ValidateStableAnchors()
        {
            foreach (var hand in _hands)
            {
                Quaternion inverse = Quaternion.Inverse(hand.Saber.transform.rotation);
                Vector3 offset = inverse * (hand.Anchor.position - hand.Saber.transform.position);
                Quaternion rotation = inverse * hand.Anchor.rotation;
                if (Vector3.Distance(offset, hand.AnchorPosition) > .001f || Quaternion.Angle(rotation, hand.AnchorRotation) > .1f)
                    throw new InvalidOperationException("The saber anchor is not fixed. Select the model's fixed root instead of a moving bone.");
            }
        }
        public void TakeControl()
        {
            foreach (var hand in _hands)
            {
                var controller = hand.Saber.GetComponentInParent<VRController>();
                if (controller == null || _trackingStates.ContainsKey(controller)) continue;
                _trackingStates.Add(controller, controller.enabled);
                controller.enabled = false;
            }
        }
        public void Apply(float songTime)
        {
            foreach (var hand in _hands) Apply(hand, songTime);
        }
        private void Apply(Hand hand, float songTime)
        {
            if (hand.Saber == null || !_clip.TryEvaluate(hand.Track, songTime, out var pose, out bool active) || !active)
                throw new InvalidOperationException("Saber data is missing at this time.");
            Quaternion rotation = new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw) * Quaternion.Inverse(hand.AnchorRotation);
            Vector3 position = new Vector3(pose.X, pose.Y, pose.Z) - rotation * hand.AnchorPosition;
            hand.Saber.OverridePositionAndRotation(position, rotation);
        }
        public void PrepareHistory(float songTime)
        {
            float now = _time.Time;
            _pausedAt = now;
            foreach (var hand in _hands)
            {
                var movement = hand.Saber.movementDataForLogic;
                var data = GameAccess.Get<BladeMovementDataElement[]>(movement, "_data"); Array.Clear(data, 0, data.Length);
                GameAccess.Set(movement, "_nextAddIndex", 0); GameAccess.Set(movement, "_validCount", 0); GameAccess.Set(movement, "_bladeSpeed", 0f);
                // Keep the same movementDataForLogic instance and its registered processors. Pending cut processors
                // have already been finished by the segment reset before this history is rebuilt.
                for (int sample = 0; sample <= 48; sample++)
                {
                    float delta = -.4f + sample / 120f;
                    Apply(hand, Mathf.Max(_clip.StartTime, songTime + delta));
                    movement.AddNewData(hand.Top.position, hand.Bottom.position, now + delta - .0001f);
                }
                Apply(hand, songTime);
                // Models and trails remain owned by the game/model provider. In particular,
                // SaberTrail subclasses can sample transforms without native movementDataForLogic.
            }
        }
        public void PauseHistory()
        {
            if (!_pausedAt.HasValue) _pausedAt = _time.Time;
        }
        public void ResumeHistory()
        {
            if (!_pausedAt.HasValue) return;
            float offset = Mathf.Max(0, _time.Time - _pausedAt.Value);
            foreach (var hand in _hands)
            {
                // Preserve unfinished after-cut ratings. AddNewData would notify those processors.
                var data = GameAccess.Get<BladeMovementDataElement[]>(hand.Saber.movementDataForLogic, "_data");
                for (int i = 0; i < data.Length; i++) data[i].time += offset;
                // Native after-cut counters compare the next sample against their own cut timestamp.
                // Rebase that timestamp too, so time spent paused cannot exhaust the 0.4 s window.
                var processors = GameAccess.Get<LazyCopyHashSet<ISaberMovementDataProcessor>>(hand.Saber.movementDataForLogic, "_dataProcessors");
                foreach (var counter in processors.items.OfType<SaberSwingRatingCounter>())
                    GameAccess.Set(counter, "_cutTime", GameAccess.Get<float>(counter, "_cutTime") + offset);
            }
            _pausedAt = null;
        }
        public void Dispose()
        {
            foreach (var pair in _trackingStates) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _trackingStates.Clear();
        }
    }
}
