using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MovementRecorder.Utility
{
    public static class UnityUtility
    {
        public static StringBuilder GetFullPathName(Transform transform, StringBuilder builder)
        {
            if (transform.parent == null)
                return builder.Append(transform.name);
            return GetFullPathName(transform.parent, builder).Append($"/{transform.name}");
        }
        public static List<(UnityEngine.Object, string)> GetFullPathNames(UnityEngine.Object[] objects)
        {
            var result = new List<(UnityEngine.Object, string)>();
            var builder = new StringBuilder(1000);
            foreach (var obj in objects)
            {
                Transform transform = obj as Transform;
                if (transform == null)
                    continue;
                builder = GetFullPathName(transform, builder);
                result.Add((obj, builder.ToString()));
                builder.Clear();
            }
            return result;
        }
        public static (List<Transform>, List<string>) FindGetTransform(List<(UnityEngine.Object, string)> allObjects, string searchStirng, List<string> exclusionStrings = null)
        {
            var resultTransform = new List<Transform>();
            var resultString = new List<string>();
            var searchRegex = new Regex(searchStirng, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var exclusionRegexs = new List<Regex>();
            if (exclusionStrings != null)
            {
                foreach (var exclusionString in exclusionStrings)
                    exclusionRegexs.Add(new Regex(exclusionString, RegexOptions.Compiled | RegexOptions.CultureInvariant));
            }
            foreach (var obj in allObjects)
                if (searchRegex.IsMatch(obj.Item2))
                {
                    var exclusion = false;
                    if (exclusionStrings != null)
                    {
                        foreach (var exclusionRegx in exclusionRegexs)
                            if (exclusionRegx.IsMatch(obj.Item2))
                            {
                                exclusion = true;
                                break;
                            }
                    }
                    if (exclusion)
                        continue;
                    Transform transform = obj.Item1 as Transform;
                    if (transform == null)
                        continue;
                    resultTransform.Add(transform);
                    resultString.Add(obj.Item2);
                }
            return (resultTransform, resultString);
        }
    }
}
