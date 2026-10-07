using UnityEngine;

namespace BetterScreenShot
{
    // Renders tiles with off-axis projections from the unmoved camera: same framing and parallax, higher pixel density.
    // Tiles are padded so post effects don't leave seams.
    internal static class RegionRenderer
    {
        private const int TileSize = 4096;
        private const int BasePadding = 64;
        private const int MaxPadding = 512;

        public static bool Capturing { get; private set; }

        public static float EffectScale { get; private set; } = 1f;

        public static Texture2D Render(Camera cam, Rect viewport, int width, int height)
        {
            // Blur planes sample the blur camera's texture in screen space, so it's re-rendered per tile too.
            Camera bgCam = null;
            Material blitMaterial = null;
            RenderTexture bgTarget = null;
            var blurredBackground = GameCameras.instance.GetComponent<LightBlurredBackground>();
            if (blurredBackground != null && blurredBackground.isActiveAndEnabled)
            {
                bgCam = BlurBackgroundAccess.BackgroundCamera(blurredBackground);
                blitMaterial = BlurBackgroundAccess.BlitMaterial(blurredBackground);
                bgTarget = bgCam != null ? bgCam.targetTexture : null;
                if (bgTarget == null)
                {
                    bgCam = null;
                }
            }

            // Frustum half-extents at distance 1.
            float tanY = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * cam.aspect;

            float scale = width / (viewport.width * cam.pixelWidth);
            int padding = Mathf.Min(MaxPadding, Mathf.CeilToInt(BasePadding * Mathf.Max(1f, scale)));

            Rect savedRect = cam.rect;
            RenderTexture savedTarget = cam.targetTexture;
            RenderTexture savedActive = RenderTexture.active;

            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            float uPerPixel = viewport.width / width;
            float vPerPixel = viewport.height / height;

            Capturing = true;
            EffectScale = scale;
            try
            {
                cam.rect = new Rect(0f, 0f, 1f, 1f);

                for (int y = 0; y < height; y += TileSize)
                {
                    for (int x = 0; x < width; x += TileSize)
                    {
                        int tileW = Mathf.Min(TileSize, width - x);
                        int tileH = Mathf.Min(TileSize, height - y);

                        float u0 = viewport.xMin + (x - padding) * uPerPixel;
                        float u1 = viewport.xMin + (x + tileW + padding) * uPerPixel;
                        float v0 = viewport.yMin + (y - padding) * vPerPixel;
                        float v1 = viewport.yMin + (y + tileH + padding) * vPerPixel;

                        RenderTexture bgTile = null;
                        if (bgCam != null)
                        {
                            // Same share of the blur texture as on screen, so the blur matches.
                            int bgW = Mathf.Max(1, Mathf.RoundToInt(bgTarget.width * (u1 - u0)));
                            int bgH = Mathf.Max(1, Mathf.RoundToInt(bgTarget.height * (v1 - v0)));
                            bgTile = RenderTexture.GetTemporary(bgW, bgH, 16, bgTarget.format);
                            bgTile.filterMode = FilterMode.Bilinear;
                            Clear(bgTile);
                            bgCam.targetTexture = bgTile;
                            bgCam.projectionMatrix = OffAxis(tanX, tanY, u0, u1, v0, v1, bgCam.nearClipPlane, bgCam.farClipPlane);
                            bgCam.Render();
                            if (blitMaterial != null)
                            {
                                blitMaterial.mainTexture = bgTile;
                            }
                        }

                        // Pooled textures keep old contents.
                        RenderTexture tile = RenderTexture.GetTemporary(tileW + 2 * padding, tileH + 2 * padding, 24, RenderTextureFormat.ARGB32);
                        Clear(tile);
                        cam.targetTexture = tile;
                        cam.projectionMatrix = OffAxis(tanX, tanY, u0, u1, v0, v1, cam.nearClipPlane, cam.farClipPlane);
                        cam.Render();

                        RenderTexture.active = tile;
                        result.ReadPixels(new Rect(padding, padding, tileW, tileH), x, y, false);
                        RenderTexture.active = savedActive;

                        cam.targetTexture = savedTarget;
                        RenderTexture.ReleaseTemporary(tile);

                        if (bgTile != null)
                        {
                            bgCam.targetTexture = bgTarget;
                            if (blitMaterial != null)
                            {
                                blitMaterial.mainTexture = bgTarget;
                            }
                            RenderTexture.ReleaseTemporary(bgTile);
                        }
                    }
                }
            }
            finally
            {
                cam.targetTexture = savedTarget;
                cam.ResetProjectionMatrix();
                cam.rect = savedRect;
                if (bgCam != null)
                {
                    bgCam.targetTexture = bgTarget;
                    bgCam.ResetProjectionMatrix();
                }
                if (blitMaterial != null && bgTarget != null)
                {
                    blitMaterial.mainTexture = bgTarget;
                }
                RenderTexture.active = savedActive;
                Capturing = false;
                EffectScale = 1f;
            }

            return result;
        }

        private static void Clear(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private static Matrix4x4 OffAxis(float tanX, float tanY, float u0, float u1, float v0, float v1, float near, float far)
        {
            return Matrix4x4.Frustum(
                (u0 * 2f - 1f) * tanX * near,
                (u1 * 2f - 1f) * tanX * near,
                (v0 * 2f - 1f) * tanY * near,
                (v1 * 2f - 1f) * tanY * near,
                near,
                far);
        }
    }
}
