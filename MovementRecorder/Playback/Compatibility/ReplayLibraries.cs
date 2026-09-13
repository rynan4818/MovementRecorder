using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MovementRecorder.Playback.Compatibility
{
    internal static class ReplayLibraries
    {
        private static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>();
        public static void Register() { AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name != "LiteDB" && name.Name != "System.Buffers") return null;
            // Supply our dependency only to this plugin or one of its embedded dependencies.
            string requesting = args.RequestingAssembly?.GetName().Name;
            if (requesting != "MovementRecorder" && requesting != "LiteDB") return null;
            lock (Loaded)
            {
                if (Loaded.TryGetValue(name.Name, out var existing)) return existing;
                using (var resource = typeof(ReplayLibraries).Assembly.GetManifestResourceStream("MovementRecorder.Dependencies." + name.Name + ".dll"))
                {
                    if (resource == null) return null;
                    using (var memory = new MemoryStream())
                    {
                        resource.CopyTo(memory); var assembly = Assembly.Load(memory.ToArray());
                        Loaded.Add(name.Name, assembly); return assembly;
                    }
                }
            }
        }
    }
}
