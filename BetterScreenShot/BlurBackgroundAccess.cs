using System.Reflection;
using UnityEngine;

namespace BetterScreenShot
{
    internal static class BlurBackgroundAccess
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo BackgroundCameraField = typeof(LightBlurredBackground).GetField("backgroundCamera", Private);
        private static readonly FieldInfo BlitMaterialField = typeof(LightBlurredBackground).GetField("blitMaterialInstance", Private);
        private static readonly FieldInfo DistantFarClipPlaneField = typeof(LightBlurredBackground).GetField("distantFarClipPlane", Private);

        public static Camera BackgroundCamera(LightBlurredBackground self) => BackgroundCameraField?.GetValue(self) as Camera;

        public static Material BlitMaterial(LightBlurredBackground self) => BlitMaterialField?.GetValue(self) as Material;

        public static float DistantFarClipPlane(LightBlurredBackground self) =>
            DistantFarClipPlaneField?.GetValue(self) is float value ? value : 1000f;
    }
}
