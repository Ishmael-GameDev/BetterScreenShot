using BetterScreenShot.Native;
using Modding;
using UnityEngine;

namespace BetterScreenShot
{
    public class BetterScreenShot : Mod, IGlobalSettings<GlobalSettings>, ITogglableMod
    {
        internal static BetterScreenShot Instance;

        public static GlobalSettings Settings { get; private set; } = new GlobalSettings();

        private GameObject _host;

        public BetterScreenShot() : base("BetterScreenShot")
        {
        }

        public override string GetVersion() => typeof(BetterScreenShot).Assembly.GetName().Version.ToString();

        public override void Initialize()
        {
            Instance = this;

            On.CameraController.LateUpdate += OnCameraControllerLateUpdate;
            On.tk2dCamera.OnPreCull += OnTk2dCameraPreCull;
            On.LightBlurredBackground.UpdateCameraClipPlanes += OnUpdateCameraClipPlanes;
            On.UnityStandardAssets.ImageEffects.FastNoise.OnRenderImage += OnFastNoiseRenderImage;
            ModHooks.CursorHook += OnCursor;
            ScaledBloom.Install();

            _host = new GameObject("BetterScreenShot");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<ScreenshotMode>();

            if (!Os.IsWindows)
            {
                Log($"Screenshot mode key: {Os.ToggleKeyName}");
                return;
            }
            PrintScreenHook.Start();
            if (PrintScreenHook.IsInstalled)
            {
                Log("PrintScreen hook installed");
            }
            else
            {
                LogWarn($"Could not install the PrintScreen hook (error {PrintScreenHook.LastError}), falling back to Unity input; Windows will still see the key");
            }
        }

        public void Unload()
        {
            On.CameraController.LateUpdate -= OnCameraControllerLateUpdate;
            On.tk2dCamera.OnPreCull -= OnTk2dCameraPreCull;
            On.LightBlurredBackground.UpdateCameraClipPlanes -= OnUpdateCameraClipPlanes;
            On.UnityStandardAssets.ImageEffects.FastNoise.OnRenderImage -= OnFastNoiseRenderImage;
            ModHooks.CursorHook -= OnCursor;
            ScaledBloom.Uninstall();

            PrintScreenHook.Stop();
            if (_host != null)
            {
                Object.Destroy(_host);
                _host = null;
            }
        }

        public void OnLoadGlobal(GlobalSettings settings) => Settings = settings;

        public GlobalSettings OnSaveGlobal() => Settings;

        private static void OnCameraControllerLateUpdate(On.CameraController.orig_LateUpdate orig, CameraController self)
        {
            if (!ScreenshotMode.Active)
            {
                orig(self);
            }
        }

        // tk2dCamera resets the projection before every render; skip that during captures.
        private static void OnTk2dCameraPreCull(On.tk2dCamera.orig_OnPreCull orig, tk2dCamera self)
        {
            if (RegionRenderer.Capturing)
            {
                tk2dUpdateManager.FlushQueues();
                return;
            }
            orig(self);
        }

        // No grain in captures: its noise texture gets recreated black at capture size, which crushes the shadows.
        private static void OnFastNoiseRenderImage(
            On.UnityStandardAssets.ImageEffects.FastNoise.orig_OnRenderImage orig,
            UnityStandardAssets.ImageEffects.FastNoise self,
            RenderTexture source,
            RenderTexture destination)
        {
            if (RegionRenderer.Capturing)
            {
                Graphics.Blit(source, destination);
                return;
            }
            orig(self, source, destination);
        }

        // Push the far planes back by the zoom-out distance so distant layers aren't cut.
        private static void OnUpdateCameraClipPlanes(On.LightBlurredBackground.orig_UpdateCameraClipPlanes orig, LightBlurredBackground self)
        {
            orig(self);

            Camera background = BlurBackgroundAccess.BackgroundCamera(self);
            if (background == null)
            {
                return;
            }
            float extra = Mathf.Max(0f, ScreenshotMode.ExtraDistance);
            float distantFar = BlurBackgroundAccess.DistantFarClipPlane(self);
            background.farClipPlane = distantFar + extra;
            if (BlurPlane.ClosestBlurPlane == null)
            {
                self.GetComponent<GameCameras>().mainCamera.farClipPlane = distantFar + extra;
            }
        }

        // API default (cursor while paused) plus the mode.
        private static void OnCursor()
        {
            GameManager gm = GameManager.UnsafeInstance;
            Cursor.visible = ScreenshotMode.Active || (gm != null && gm.isPaused);
        }
    }
}
