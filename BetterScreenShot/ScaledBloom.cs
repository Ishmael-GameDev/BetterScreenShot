using System.Reflection;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace BetterScreenShot
{
    // Keeps the bloom's downsampled copy the same size relative to the scene as in game, so the glow doesn't shrink at high resolution.
    internal static class ScaledBloom
    {
        private delegate void OrigOnRenderImage(BloomOptimized self, RenderTexture source, RenderTexture destination);

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo MaterialField = typeof(BloomOptimized).GetField("fastBloomMaterial", Private);
        private static readonly FieldInfo ResolutionField = typeof(BloomOptimized).GetField("resolution", Private);

        private static Hook _hook;

        public static void Install()
        {
            MethodInfo target = typeof(BloomOptimized).GetMethod("OnRenderImage", Private);
            if (target == null || MaterialField == null || ResolutionField == null)
            {
                BetterScreenShot.Instance.LogWarn("BloomOptimized has changed, captures at native resolution will have a thinner glow");
                return;
            }
            _hook = new Hook(target, typeof(ScaledBloom).GetMethod(nameof(OnRenderImage), BindingFlags.Static | BindingFlags.NonPublic));
        }

        public static void Uninstall()
        {
            _hook?.Dispose();
            _hook = null;
        }

        private static void OnRenderImage(OrigOnRenderImage orig, BloomOptimized self, RenderTexture source, RenderTexture destination)
        {
            float scale = RegionRenderer.EffectScale;
            if (!RegionRenderer.Capturing || Mathf.Abs(scale - 1f) < 0.01f || !self.CheckResources())
            {
                orig(self, source, destination);
                return;
            }

            var material = (Material)MaterialField.GetValue(self);
            bool low = (BloomOptimized.Resolution)ResolutionField.GetValue(self) == BloomOptimized.Resolution.Low;
            int downsample = low ? 4 : 2;
            float sizeFactor = low ? 0.5f : 1f;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width / (downsample * scale)));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height / (downsample * scale)));

            // Halve down to about screen size first so the 4-tap threshold pass doesn't skip pixels.
            source.filterMode = FilterMode.Bilinear;
            RenderTexture current = source;
            while (current.width / 2 >= width * downsample && current.height / 2 >= height * downsample)
            {
                RenderTexture half = RenderTexture.GetTemporary(current.width / 2, current.height / 2, 0, source.format);
                half.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, half);
                if (current != source)
                {
                    RenderTexture.ReleaseTemporary(current);
                }
                current = half;
            }

            // Same as BloomOptimized.OnRenderImage from here.
            material.SetVector("_Parameter", new Vector4(self.blurSize * sizeFactor, 0f, self.threshold, self.intensity));
            RenderTexture bloom = RenderTexture.GetTemporary(width, height, 0, source.format);
            bloom.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, bloom, material, 1);
            if (current != source)
            {
                RenderTexture.ReleaseTemporary(current);
            }

            int passOffset = self.blurType != BloomOptimized.BlurType.Standard ? 2 : 0;
            for (int i = 0; i < self.blurIterations; i++)
            {
                material.SetVector("_Parameter", new Vector4(self.blurSize * sizeFactor + i, 0f, self.threshold, self.intensity));
                RenderTexture vertical = RenderTexture.GetTemporary(width, height, 0, source.format);
                vertical.filterMode = FilterMode.Bilinear;
                Graphics.Blit(bloom, vertical, material, 2 + passOffset);
                RenderTexture.ReleaseTemporary(bloom);

                RenderTexture horizontal = RenderTexture.GetTemporary(width, height, 0, source.format);
                horizontal.filterMode = FilterMode.Bilinear;
                Graphics.Blit(vertical, horizontal, material, 3 + passOffset);
                RenderTexture.ReleaseTemporary(vertical);
                bloom = horizontal;
            }

            material.SetTexture("_Bloom", bloom);
            Graphics.Blit(source, destination, material, 0);
            RenderTexture.ReleaseTemporary(bloom);
        }
    }
}
