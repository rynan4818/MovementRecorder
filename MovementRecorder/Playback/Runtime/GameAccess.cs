using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MovementRecorder.Playback.Runtime
{
    internal static class GameAccess
    {
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();
        public static FieldInfo Field(Type type, string name)
        {
            string key = type.AssemblyQualifiedName + ":" + name;
            if (!Fields.TryGetValue(key, out var field))
                Fields[key] = field = AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
            return field;
        }
        public static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        public static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        public static MethodInfo Method(Type type, string name)
        {
            string key = type.AssemblyQualifiedName + ":" + name;
            if (!Methods.TryGetValue(key, out var method))
                Methods[key] = method = AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
            return method;
        }
        public static object Call(object target, string name, params object[] args) => Method(target.GetType(), name).Invoke(target, args);
    }
}
