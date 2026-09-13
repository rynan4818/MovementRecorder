using System;
using MovementRecorder.Models;

namespace MovementRecorder.Playback.Data
{
    // The on-disk pose is in Beat Saber world coordinates. No editor offsets belong here.
    internal struct RecordedPose
    {
        public float X, Y, Z, Qx, Qy, Qz, Qw;

        public bool IsFinite => Number.IsFinite(X) && Number.IsFinite(Y) && Number.IsFinite(Z) &&
            Number.IsFinite(Qx) && Number.IsFinite(Qy) && Number.IsFinite(Qz) && Number.IsFinite(Qw);

        public bool NormalizeRotation()
        {
            double length = (double)Qx * Qx + (double)Qy * Qy + (double)Qz * Qz + (double)Qw * Qw;
            if (length < 1e-12 || double.IsInfinity(length) || double.IsNaN(length)) return false;
            float inverse = (float)(1 / Math.Sqrt(length));
            Qx *= inverse; Qy *= inverse; Qz *= inverse; Qw *= inverse;
            return true;
        }

        public static RecordedPose Interpolate(RecordedPose a, RecordedPose b, float amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            double dot = (double)a.Qx * b.Qx + (double)a.Qy * b.Qy + (double)a.Qz * b.Qz + (double)a.Qw * b.Qw;
            if (dot < 0) { dot = -dot; b.Qx = -b.Qx; b.Qy = -b.Qy; b.Qz = -b.Qz; b.Qw = -b.Qw; }
            double left = 1 - amount, right = amount;
            if (dot < 0.9995)
            {
                double angle = Math.Acos(Math.Max(-1, Math.Min(1, dot)));
                double sine = Math.Sin(angle);
                left = Math.Sin((1 - amount) * angle) / sine;
                right = Math.Sin(amount * angle) / sine;
            }
            var result = new RecordedPose
            {
                X = (float)(a.X + ((double)b.X - a.X) * amount), Y = (float)(a.Y + ((double)b.Y - a.Y) * amount),
                Z = (float)(a.Z + ((double)b.Z - a.Z) * amount),
                Qx = (float)(a.Qx * left + b.Qx * right), Qy = (float)(a.Qy * left + b.Qy * right),
                Qz = (float)(a.Qz * left + b.Qz * right), Qw = (float)(a.Qw * left + b.Qw * right)
            };
            result.NormalizeRotation();
            return result;
        }
    }

    internal static class Number
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    internal sealed class MovementClip
    {
        public MovementJson Header { get; }
        public MovementFileMetadata Metadata { get; }
        private readonly float[] _times;
        private readonly RecordedPose[] _poses;
        private readonly float[] _missingFrom;
        private readonly int[] _lastValid;
        public int FrameCount => _times.Length;
        public int TrackCount => Header.objectCount;
        public float StartTime => _times[0];
        public float EndTime => _times[_times.Length - 1];
        public long DataBytes => (long)_poses.Length * 28 + (long)_times.Length * 4;

        internal MovementClip(MovementJson header, MovementFileMetadata metadata, float[] times,
            RecordedPose[] poses, float[] missingFrom)
        {
            Header = header; Metadata = metadata; _times = times; _poses = poses; _missingFrom = missingFrom;
            _lastValid = new int[TrackCount];
            for (int i = 0; i < TrackCount; i++)
                _lastValid[i] = float.IsPositiveInfinity(missingFrom[i]) ? FrameCount - 1 : LowerBound(missingFrom[i]) - 1;
        }

        public float FrameTime(int index) => _times[index];

        public bool TryEvaluate(int track, float time, out RecordedPose pose, out bool active)
        {
            pose = default(RecordedPose); active = false;
            if (track < 0 || track >= TrackCount || !Number.IsFinite(time) || _lastValid[track] < 0) return false;
            active = time < _missingFrom[track];
            time = Math.Max(StartTime, Math.Min(EndTime, time));
            int left = Math.Min(_lastValid[track], Math.Max(0, UpperBound(time) - 1));
            int right = Math.Min(_lastValid[track], left + 1);
            pose = _poses[left * TrackCount + track];
            if (left == right) return true;
            double delta = (double)_times[right] - _times[left];
            float amount = delta <= 0 ? 1 : (float)(((double)time - _times[left]) / delta);
            pose = RecordedPose.Interpolate(pose, _poses[right * TrackCount + track], amount);
            return true;
        }

        private int UpperBound(float time)
        {
            int low = 0, high = _times.Length;
            while (low < high) { int mid = low + (high - low) / 2; if (_times[mid] <= time) low = mid + 1; else high = mid; }
            return low;
        }

        private int LowerBound(float time)
        {
            int low = 0, high = _times.Length;
            while (low < high) { int mid = low + (high - low) / 2; if (_times[mid] < time) low = mid + 1; else high = mid; }
            return low;
        }
    }
}
