using System;
using System.Reflection;
using System.Text;
using BetterScreenShot.Native;
using UnityEngine;

namespace BetterScreenShot
{
    internal static class Diagnostics
    {
        public static string Report(Camera cam, float zoom, float baseDistance)
        {
            var report = new StringBuilder();
            report.AppendLine($"BetterScreenShot {typeof(Diagnostics).Assembly.GetName().Version}, Unity {Application.unityVersion}, {Application.platform}");
            report.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            report.AppendLine($"Screen: {Screen.width}x{Screen.height}, mode {Screen.fullScreenMode}, color space {QualitySettings.activeColorSpace}, AA {QualitySettings.antiAliasing}");
            report.AppendLine($"Zoom: {zoom:0.###}, base distance {baseDistance:0.###}, PrintScreen hook: {PrintScreenHook.IsInstalled}, masks hidden: {VisualMasks.IsHidden}");
            report.AppendLine();

            report.AppendLine("== Main camera ==");
            AppendCamera(report, cam);
            report.AppendLine($"  position {cam.transform.position}, fov {cam.fieldOfView}, aspect {cam.aspect}, pixelRect {cam.pixelRect}");
            report.AppendLine($"  renderingPath {cam.actualRenderingPath}, depthTextureMode {cam.depthTextureMode}, transparencySort {cam.transparencySortMode}");
            report.AppendLine("  Components (in effect order):");
            foreach (Component component in cam.GetComponents<Component>())
            {
                AppendComponent(report, component);
            }
            report.AppendLine();

            report.AppendLine("== All cameras ==");
            foreach (Camera other in Resources.FindObjectsOfTypeAll<Camera>())
            {
                // Skip prefabs.
                if (other.gameObject.scene.IsValid())
                {
                    AppendCamera(report, other);
                }
            }

            report.AppendLine();
            report.AppendLine("== Blur planes ==");
            report.AppendLine($"  count {BlurPlane.BlurPlaneCount}, closest z {(BlurPlane.ClosestBlurPlane != null ? BlurPlane.ClosestBlurPlane.PlaneZ.ToString() : "none")}");
            var blurred = GameCameras.instance.GetComponent<LightBlurredBackground>();
            if (blurred != null)
            {
                Camera bg = BlurBackgroundAccess.BackgroundCamera(blurred);
                report.AppendLine($"  LightBlurredBackground enabled {blurred.isActiveAndEnabled}, distantFar {BlurBackgroundAccess.DistantFarClipPlane(blurred)}");
                if (bg != null)
                {
                    AppendCamera(report, bg);
                }
            }
            return report.ToString();
        }

        private static void AppendCamera(StringBuilder report, Camera cam)
        {
            RenderTexture target = cam.targetTexture;
            report.AppendLine($"  [{cam.name}] enabled {cam.enabled}/{cam.gameObject.activeInHierarchy}, depth {cam.depth}, clear {cam.clearFlags} {cam.backgroundColor}, "
                + $"HDR {cam.allowHDR}, MSAA {cam.allowMSAA}, ortho {cam.orthographic}, near {cam.nearClipPlane:0.##}, far {cam.farClipPlane:0.##}, rect {cam.rect}, "
                + $"mask 0x{cam.cullingMask:X8}, target {(target != null ? $"{target.width}x{target.height} {target.format}" : "screen")}");
        }

        private static void AppendComponent(StringBuilder report, Component component)
        {
            Type type = component.GetType();
            string enabled = component is Behaviour behaviour ? (behaviour.enabled ? "on" : "OFF") : "-";
            report.Append($"    {type.FullName} [{enabled}]");
            if (component is MonoBehaviour)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    Type ft = field.FieldType;
                    if (ft.IsPrimitive || ft.IsEnum || ft == typeof(Color) || ft == typeof(Vector2) || ft == typeof(Vector3) || ft == typeof(Vector4))
                    {
                        report.Append($" {field.Name}={field.GetValue(component)}");
                    }
                }
            }
            report.AppendLine();
        }
    }
}
