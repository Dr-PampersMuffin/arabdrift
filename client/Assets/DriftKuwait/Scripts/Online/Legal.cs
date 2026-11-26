using UnityEngine;

namespace DriftKuwait
{
    /// The public pages the App Store listing links to (served by GitHub Pages from the repo's gh-pages branch;
    /// sources in site/). Opened in Safari from Settings and from the pearl page.
    public static class Legal
    {
        public const string Base = "https://dr-pampersmuffin.github.io/arabdrift/";
        public const string Privacy = Base + "privacy.html";
        public const string Terms = Base + "terms.html";
        public const string Support = Base + "support.html";
        public const string AppleEula = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";

        public static void Open(string url)
        {
            Sfx.Play("ui_click");
            Application.OpenURL(url);
        }
    }
}
