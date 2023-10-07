using UnityEngine;

namespace MovementRecorder.Utility
{
    public static class UnityUtility
    {
        public static string GetFullPathName(this Transform transform)
        {
            if (transform.parent == null)
                return transform.name;
            return GetFullPathName(transform.parent) + "/" + transform.name;
        }
    }
}
