using System.Collections.Generic;
using UnityEngine;

namespace BetterScreenShot
{
    // Same mask detection as DebugMod; only renderers hidden here get restored.
    internal static class VisualMasks
    {
        private static readonly List<Renderer> Hidden = new List<Renderer>();

        public static bool IsHidden => Hidden.Count > 0;

        public static void Hide()
        {
            HeroController hero = HeroController.instance;
            if (hero == null || IsHidden)
            {
                return;
            }

            float heroZ = hero.transform.position.z;
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
            {
                // Masks are in front of the gameplay plane.
                if (go.transform.position.z <= heroZ && IsMask(go))
                {
                    HideRenderers(go);
                }
            }
            if (hero.vignette != null)
            {
                HideRenderers(hero.vignette.gameObject);
            }
        }

        public static void Restore()
        {
            foreach (Renderer renderer in Hidden)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            Hidden.Clear();
        }

        private static bool IsMask(GameObject go)
        {
            string name = go.name;
            if (name.StartsWith("msk_") || name.StartsWith("Tut_msk") || name.StartsWith("black_solid"))
            {
                return true;
            }
            string lower = name.ToLowerInvariant();
            if (lower.Contains("vignette") || lower.Contains("secret mask"))
            {
                return true;
            }
            return go.LocateMyFSM("unmasker") != null
                || go.LocateMyFSM("remasker_inverse") != null
                || go.LocateMyFSM("remasker") != null;
        }

        private static void HideRenderers(GameObject go)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    Hidden.Add(renderer);
                }
            }
        }
    }
}
