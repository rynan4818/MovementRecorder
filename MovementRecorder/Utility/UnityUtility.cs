using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public static List<(Transform, string)> GetFullPathNames(Transform[] transforms)
        {
            List<(Transform, string)> result = new List<(Transform, string)>();
            foreach(var transform in transforms)
                result.Add((transform , transform.GetFullPathName()));
            return result;
        }
        public static (List<Transform>, List<string>) FindGetTransform(List<(Transform, string)> allTransforms, string searchStirng, List<string> exclusionStrings = null)
        {
            List<Transform> resultTransform = new List<Transform>();
            List<string> resultString = new List<string>();
            var searchRegex = new Regex(searchStirng, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            List<Regex> exclusionRegexs = new List<Regex>();
            if (exclusionStrings != null)
            {
                foreach (var exclusionString in exclusionStrings)
                    exclusionRegexs.Add(new Regex(exclusionString, RegexOptions.Compiled | RegexOptions.CultureInvariant));
            }
            foreach (var transform in allTransforms)
                if (searchRegex.IsMatch(transform.Item2))
                {
                    var exclusion = false;
                    if (exclusionStrings != null)
                    {
                        foreach (var exclusionRegx in exclusionRegexs)
                            if (exclusionRegx.IsMatch(transform.Item2))
                            {
                                exclusion = true;
                                break;
                            }
                    }
                    if (exclusion)
                        continue;
                    resultTransform.Add(transform.Item1);
                    resultString.Add(transform.Item2);
                }
            return (resultTransform, resultString);
        }
    }
}
