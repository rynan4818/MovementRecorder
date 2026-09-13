using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MovementRecorder.Playback.Runtime
{
    internal sealed class NativeSegmentState
    {
        private readonly ScoreController _score;
        private readonly ComboController _combo;
        private readonly GameEnergyCounter _energy;
        private readonly PlayerHeadAndObstacleInteraction _head;
        private readonly BeatmapObjectExecutionRatingsRecorder _ratings;
        private readonly float _initialEnergy;
        private readonly int _initialLives;
        private readonly List<ScoringElement>[] _scoringLists;
        private readonly List<float> _noteTimes;
        public NativeSegmentState(ScoreController score, ComboController combo, GameEnergyCounter energy,
            PlayerHeadAndObstacleInteraction head, BeatmapObjectExecutionRatingsRecorder ratings)
        {
            _score = score; _combo = combo; _energy = energy; _head = head; _ratings = ratings;
            _initialEnergy = energy.energy; _initialLives = GameAccess.Get<int>(energy, "_batteryLives");
            _scoringLists = new[] { "_sortedScoringElementsWithoutMultiplier", "_scoringElementsWithMultiplier", "_scoringElementsToRemove" }
                .Select(n => GameAccess.Get<List<ScoringElement>>(score, n)).ToArray();
            _noteTimes = GameAccess.Get<List<float>>(score, "_sortedNoteTimesWithoutScoringElements");
            foreach (string name in new[] { "_modifiedScore", "_multipliedScore", "_immediateMaxPossibleMultipliedScore", "_immediateMaxPossibleModifiedScore" }) GameAccess.Field(typeof(ScoreController), name);
            GameAccess.Field(typeof(GameEnergyCounter), "<energy>k__BackingField");
        }
        public void RemoveExpiredNote(float time) { _noteTimes.Remove(time); }
        public void Reset()
        {
            foreach (var scoring in _scoringLists.SelectMany(l => l).Distinct().ToArray())
            {
                if (scoring is GoodCutScoringElement good && !good.isFinished)
                {
                    var buffer = GameAccess.Get<CutScoreBuffer>(good, "_cutScoreBuffer");
                    GameAccess.Get<SaberSwingRatingCounter>(buffer, "_saberSwingRatingCounter").Finish();
                }
                _score.DespawnScoringElement(scoring);
            }
            foreach (var list in _scoringLists) list.Clear(); _noteTimes.Clear();
            foreach (string name in new[] { "_modifiedScore", "_multipliedScore", "_immediateMaxPossibleMultipliedScore", "_immediateMaxPossibleModifiedScore" }) GameAccess.Set(_score, name, 0);
            GameAccess.Get<ScoreMultiplierCounter>(_score, "_scoreMultiplierCounter").Reset();
            GameAccess.Get<ScoreMultiplierCounter>(_score, "_maxScoreMultiplierCounter").Reset();
            GameAccess.Set(_combo, "_combo", 0); GameAccess.Set(_combo, "_maxCombo", 0);
            GameAccess.Set(_energy, "<energy>k__BackingField", _initialEnergy); GameAccess.Set(_energy, "_batteryLives", _initialLives);
            GameAccess.Set(_energy, "_didReach0Energy", false); GameAccess.Set(_energy, "_nextFrameEnergyChange", 0f);
            GameAccess.Get<HashSet<ObstacleController>>(_head, "_intersectingObstacles").Clear();
            GameAccess.Set(_head, "_lastFrameNumCheck", -1); GameAccess.Set(_head, "_prevFrameNumberOfIntersectingObstaclesCount", 0);
            GameAccess.Get<IList>(_ratings, "_beatmapObjectExecutionRatings").Clear();
            GameAccess.Get<HashSet<ObstacleController>>(_ratings, "_hitObstacles").Clear();
            // Value notifications update the HUD; no GoodCut, Miss or combo-breaking event is synthesized.
            GameAccess.Get<Action<int, int>>(_score, "scoreDidChangeEvent")?.Invoke(0, 0);
            GameAccess.Get<Action<int, float>>(_score, "multiplierDidChangeEvent")?.Invoke(1, 0f);
            GameAccess.Get<Action<int>>(_combo, "comboDidChangeEvent")?.Invoke(0);
            GameAccess.Get<Action<float>>(_energy, "gameEnergyDidChangeEvent")?.Invoke(_initialEnergy);
        }
    }
}
