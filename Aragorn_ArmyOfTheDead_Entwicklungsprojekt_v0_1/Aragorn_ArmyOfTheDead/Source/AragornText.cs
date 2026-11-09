using System;
using Verse;

namespace AragornArmy
{
    // RimWorld's native keyed language packs: English source + German localization.
    internal static class AragornText
    {
        public static string T(string key) => key.TranslateSimple();
        public static string F(string key, params object[] args) =>
            string.Format(T(key), args);
    }
}
