using UnityEngine;

namespace DriftKuwait
{
    /// Sodium-orange street lamp; lights only at night to save the GPU by day.
    public class StreetLamp : MonoBehaviour
    {
        public Light lamp;
        public Renderer bulb;
        public void SetOn(bool on)
        {
            if (lamp != null) lamp.enabled = on;
            if (bulb != null) bulb.sharedMaterial = on ? Mat.Glow("Sodium_On", new Color(1f, 0.6f, 0.22f), 6f)
                                                         : Mat.Paint("Sodium_Off", new Color(0.55f, 0.5f, 0.42f), 0.6f);
        }
    }
}
