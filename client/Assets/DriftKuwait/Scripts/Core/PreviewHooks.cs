using System;
using System.Collections;
using System.Collections.Generic;

namespace DriftKuwait
{
    /// Lets the cloud preview test (Tests/PreviewCapture.cs) open screens and switch lighting without the game
    /// depending on the test assembly. Screens register their shots here; the test captures them in order.
    public static class PreviewHooks
    {
        public static bool Active;

        public class Shot
        {
            public string name;
            public Func<IEnumerator> setup;
            public Action teardown;
        }

        public static readonly List<Shot> MenuShots = new List<Shot>();
        public static Action<bool> SetNight;

        public static void Register(string name, Func<IEnumerator> setup, Action teardown = null)
        {
            MenuShots.RemoveAll(s => s.name == name);
            MenuShots.Add(new Shot { name = name, setup = setup, teardown = teardown });
            MenuShots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }
    }
}
