using System;
using System.Collections;
using System.IO;
using BetterScreenShot.Native;
using GlobalEnums;
using UnityEngine;

namespace BetterScreenShot
{
    internal class ScreenshotMode : MonoBehaviour
    {
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 25f;
        private const float DragThreshold = 6f;
        private const float MainPanelWidth = 620f;
        private const float TimePanelWidth = 720f;

        private enum SelectionState { None, Placing, Done }

        private enum Output { Clipboard, File }

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color OkColor = new Color(0.55f, 0.95f, 0.55f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f);

        public static bool Active { get; private set; }

        public static float ExtraDistance { get; private set; }

        private static GlobalSettings Settings => BetterScreenShot.Settings;

        // Saved on entering the mode.
        private Vector3 _savedCameraPosition;
        private bool _savedInputAccepting;
        private bool _savedHeroAccepting;
        private DamageMode _savedDamageMode;
        private bool _savedHudCameraEnabled;
        // On at every game start, so not saved.
        private static bool _hideMasks = true;
        private bool _restoringInput;

        private float _baseDistance;
        private float _zoom = 1f;
        private readonly TimeControl _time = new TimeControl();

        private SelectionState _selection;
        // Room coordinates on the gameplay plane.
        private Vector3 _cornerA;
        private Vector3 _cornerB;
        private float _planeZ;
        private Vector2 _pressPosition;
        private bool _leftPressInView;
        private bool _ignoreNextRelease;

        private int _panButton = -1;
        private bool _panning;
        private Vector2 _panPressPosition;
        private Vector2 _lastMouse;

        private bool _busy;
        private bool _hideUi;
        private string _status;
        private Color _statusColor = Color.white;

        private readonly Panel _mainPanel = new Panel(MainPanelWidth, new Vector2(20f, 20f));
        private readonly Panel _timePanel = new Panel(TimePanelWidth, Vector2.zero);
        private bool _timePanelPlaced;
        private float _guiScale = 1f;
        private Styles _styles;

        private static Camera MainCamera => GameCameras.instance.mainCamera;

        private static Transform CameraRig => GameCameras.instance.cameraController.transform;

        private static Rect View => MainCamera.pixelRect;

        private static float TanHalfFovY => Mathf.Tan(MainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        private float WorldUnitsPerPixel => 2f * TanHalfFovY * _baseDistance * _zoom / View.height;

        private Vector2 GuiScreenSize => new Vector2(Screen.width, Screen.height) / _guiScale;

        private void Update()
        {
            KeyInput.Update();

            bool canEnter = !Active && !_restoringInput && CanEnter();
            PrintScreenHook.Intercept = Active || canEnter;
            bool toggle = ToggleKeyPressed();

            if (!Active)
            {
                if (toggle && canEnter)
                {
                    Enter();
                }
                return;
            }

            GameManager gm = GameManager.UnsafeInstance;
            if (gm == null || gm.gameState != GameState.PLAYING || HeroController.instance == null || GameCameras.instance == null)
            {
                // E.g. the Knight walked into a transition while time ran.
                Exit();
                return;
            }

            _time.Update();

            if (_busy)
            {
                return;
            }

            if (KeyInput.WasPressed(Key.Escape) && _selection == SelectionState.Placing)
            {
                _selection = SelectionState.None;
            }
            else if (toggle || KeyInput.WasPressed(Key.Escape))
            {
                Exit();
                return;
            }

            _guiScale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 3f);
            HandlePanels();
            HandleCamera();
            HandleSelection();
            HandleHotkeys();
        }

        private static bool ToggleKeyPressed()
        {
            if (Os.IsWindows && PrintScreenHook.IsInstalled)
            {
                return PrintScreenHook.ConsumePresses() > 0;
            }
            if (Os.IsMac)
            {
                return KeyInput.IsHeld(Key.Modifier) && KeyInput.IsHeld(Key.Shift) && KeyInput.WasPressed(Key.Two);
            }
            return Input.GetKeyDown(KeyCode.Print) || Input.GetKeyDown(KeyCode.SysReq);
        }

        private static bool CanEnter()
        {
            GameManager gm = GameManager.UnsafeInstance;
            return gm != null
                && gm.gameState == GameState.PLAYING
                && !gm.isPaused
                && gm.IsGameplayScene()
                && HeroController.instance != null
                && GameCameras.instance != null
                && GameCameras.instance.mainCamera != null;
        }

        private void Enter()
        {
            HeroController hero = HeroController.instance;
            GameCameras cameras = GameCameras.instance;

            _savedInputAccepting = InputHandler.Instance.acceptingInput;
            InputHandler.Instance.acceptingInput = false;
            _savedHeroAccepting = hero.acceptingInput;
            hero.acceptingInput = false;
            // Time can run in the mode.
            _savedDamageMode = hero.damageMode;
            hero.damageMode = DamageMode.NO_DAMAGE;

            _savedCameraPosition = CameraRig.position;
            _baseDistance = -MainCamera.transform.position.z;
            if (_baseDistance < 1f)
            {
                _baseDistance = 38.1f;
            }
            _zoom = 1f;
            ExtraDistance = 0f;

            _savedHudCameraEnabled = cameras.hudCamera.enabled;
            cameras.hudCamera.enabled = false;

            _selection = SelectionState.None;
            _panButton = -1;
            _status = null;
            _busy = false;
            _planeZ = hero.transform.position.z;
            Active = true;
            ApplyMasks();
            _time.Reset();
            StartCoroutine(EndOfFrameLoop());
        }

        private void Exit(bool restoreInputNow = false)
        {
            Active = false;
            _busy = false;
            ExtraDistance = 0f;

            GameCameras cameras = GameCameras.instance;
            if (cameras != null)
            {
                CameraRig.position = _savedCameraPosition;
                cameras.hudCamera.enabled = _savedHudCameraEnabled;
            }

            HeroController hero = HeroController.instance;
            if (hero != null)
            {
                hero.damageMode = _savedDamageMode;
            }

            VisualMasks.Restore();
            TimeControl.Release();

            if (restoreInputNow)
            {
                RestoreInput(hero);
            }
            else
            {
                // A frame later, so the closing Esc doesn't open the pause menu.
                StartCoroutine(RestoreInputNextFrame(hero));
            }
        }

        private IEnumerator EndOfFrameLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (Active)
            {
                yield return endOfFrame;
                if (Active)
                {
                    _time.EndOfFrame();
                }
            }
        }

        private IEnumerator RestoreInputNextFrame(HeroController hero)
        {
            _restoringInput = true;
            yield return null;
            RestoreInput(hero);
        }

        private void RestoreInput(HeroController hero)
        {
            if (InputHandler.Instance != null)
            {
                InputHandler.Instance.acceptingInput = _savedInputAccepting;
            }
            if (hero != null)
            {
                hero.acceptingInput = _savedHeroAccepting;
            }
            _restoringInput = false;
        }

        private void OnDestroy()
        {
            if (Active)
            {
                Exit(restoreInputNow: true);
            }
            _styles?.Destroy();
        }

        private static void ApplyMasks()
        {
            if (_hideMasks)
            {
                VisualMasks.Hide();
            }
            else
            {
                VisualMasks.Restore();
            }
        }

        #region Panels

        private Vector2 GuiMouse => ToGui((Vector2)Input.mousePosition) / _guiScale;

        private void HandlePanels()
        {
            Vector2 screen = GuiScreenSize;
            if (!_timePanelPlaced)
            {
                _timePanel.MoveTo(new Vector2((screen.x - TimePanelWidth) / 2f, screen.y - _timePanel.Height - 20f));
                _timePanelPlaced = true;
            }

            Vector2 mouse = GuiMouse;
            bool pressed = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);
            _mainPanel.HandleDrag(mouse, screen, pressed, held);
            _timePanel.HandleDrag(mouse, screen, pressed && !_mainPanel.IsDragging, held);
        }

        private bool IsOverPanel(Vector2 screenPoint)
        {
            Vector2 gui = ToGui(screenPoint) / _guiScale;
            return _mainPanel.Contains(gui) || _timePanel.Contains(gui);
        }

        #endregion

        #region Camera

        private void HandleCamera()
        {
            Vector2 mouse = Input.mousePosition;
            bool overPanel = IsOverPanel(mouse);

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f && !overPanel)
            {
                SetZoom(_zoom * Mathf.Pow(1.15f, -scroll), mouse);
            }
            if (KeyInput.WasPressed(Key.Plus))
            {
                SetZoom(_zoom / 1.25f, View.center);
            }
            if (KeyInput.WasPressed(Key.Minus))
            {
                SetZoom(_zoom * 1.25f, View.center);
            }

            // RMB/MMB drag pans; a plain right click clears the selection.
            for (int button = 1; button <= 2; button++)
            {
                if (_panButton < 0 && Input.GetMouseButtonDown(button) && !overPanel)
                {
                    _panButton = button;
                    _panPressPosition = mouse;
                    _lastMouse = mouse;
                    _panning = false;
                }
            }
            if (_panButton >= 0)
            {
                if (Input.GetMouseButton(_panButton))
                {
                    if (!_panning && (mouse - _panPressPosition).magnitude > DragThreshold)
                    {
                        _panning = true;
                        _lastMouse = _panPressPosition;
                    }
                    if (_panning)
                    {
                        MoveCamera(-(mouse - _lastMouse) * WorldUnitsPerPixel);
                    }
                    _lastMouse = mouse;
                }
                else
                {
                    if (!_panning && _panButton == 1)
                    {
                        _selection = SelectionState.None;
                    }
                    _panButton = -1;
                    _panning = false;
                }
            }

            if (KeyInput.IsHeld(Key.Modifier))
            {
                return;
            }
            Vector2 direction = Vector2.zero;
            if (KeyInput.IsHeld(Key.Left) || KeyInput.IsHeld(Key.A)) direction.x -= 1f;
            if (KeyInput.IsHeld(Key.Right) || KeyInput.IsHeld(Key.D)) direction.x += 1f;
            if (KeyInput.IsHeld(Key.Down) || KeyInput.IsHeld(Key.S)) direction.y -= 1f;
            if (KeyInput.IsHeld(Key.Up) || KeyInput.IsHeld(Key.W)) direction.y += 1f;
            if (direction != Vector2.zero)
            {
                float screensPerSecond = KeyInput.IsHeld(Key.Shift) ? 1.5f : 0.5f;
                MoveCamera(direction.normalized * (screensPerSecond * View.height * WorldUnitsPerPixel * Time.unscaledDeltaTime));
            }
        }

        private static void MoveCamera(Vector2 worldDelta)
        {
            CameraRig.position += new Vector3(worldDelta.x, worldDelta.y, 0f);
        }

        // Keeps the point under the anchor in place.
        private void SetZoom(float zoom, Vector2 anchor)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            Rect view = View;
            float tanY = TanHalfFovY;
            float tanX = tanY * MainCamera.aspect;
            float u = (anchor.x - view.x) / view.width;
            float v = (anchor.y - view.y) / view.height;
            float distanceChange = _baseDistance * (_zoom - zoom);

            Vector3 position = CameraRig.position;
            position.x += (u - 0.5f) * 2f * tanX * distanceChange;
            position.y += (v - 0.5f) * 2f * tanY * distanceChange;
            position.z = _savedCameraPosition.z - (zoom - 1f) * _baseDistance;
            CameraRig.position = position;

            _zoom = zoom;
            ExtraDistance = (zoom - 1f) * _baseDistance;
        }

        #endregion

        #region Selection

        private void HandleSelection()
        {
            Vector2 raw = Input.mousePosition;
            Vector2 mouse = ClampToView(raw);

            if (Input.GetMouseButtonDown(0))
            {
                _leftPressInView = !IsOverPanel(raw);
                if (_leftPressInView)
                {
                    if (_selection == SelectionState.Placing)
                    {
                        _cornerB = ScreenToWorld(mouse);
                        _selection = SelectionState.Done;
                        _ignoreNextRelease = true;
                    }
                    else
                    {
                        _cornerA = _cornerB = ScreenToWorld(mouse);
                        _pressPosition = mouse;
                        _selection = SelectionState.Placing;
                        _ignoreNextRelease = false;
                    }
                }
            }

            if (_selection == SelectionState.Placing)
            {
                _cornerB = ScreenToWorld(mouse);
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_ignoreNextRelease)
                {
                    _ignoreNextRelease = false;
                }
                else if (_leftPressInView && _selection == SelectionState.Placing
                    && (mouse - _pressPosition).magnitude > DragThreshold)
                {
                    // Released after a drag.
                    _selection = SelectionState.Done;
                }
                _leftPressInView = false;
            }

            if (_selection == SelectionState.Done)
            {
                Rect selection = SelectionRect;
                if (selection.width < 1f || selection.height < 1f)
                {
                    _selection = SelectionState.None;
                }
            }
        }

        private void HandleHotkeys()
        {
            if (KeyInput.IsHeld(Key.Modifier))
            {
                if (KeyInput.WasPressed(Key.C))
                {
                    StartCapture(Output.Clipboard);
                }
                else if (KeyInput.WasPressed(Key.S))
                {
                    StartCapture(Output.File);
                }
                else if (KeyInput.WasPressed(Key.A))
                {
                    SelectAll();
                }
                return;
            }

            if (KeyInput.WasPressed(Key.F9))
            {
                _busy = true;
                StartCoroutine(SaveDiagnostics());
                return;
            }
            if (KeyInput.WasPressed(Key.Backspace))
            {
                _selection = SelectionState.None;
            }
            if (KeyInput.WasPressed(Key.Space))
            {
                _time.TogglePlay();
            }
            if (KeyInput.WasPressed(Key.Period))
            {
                _time.Step(KeyInput.IsHeld(Key.Shift) ? 10 : 1);
            }
        }

        private void SelectAll()
        {
            Rect view = View;
            _cornerA = ScreenToWorld(view.min);
            _cornerB = ScreenToWorld(view.max);
            _selection = SelectionState.Done;
        }

        // Corners are in room coordinates, so the rectangle follows zoom and panning and may leave the screen.
        private Rect SelectionRect
        {
            get
            {
                Vector2 a = WorldToScreen(_cornerA);
                Vector2 b = WorldToScreen(_cornerB);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        private Vector3 ScreenToWorld(Vector2 screenPoint)
        {
            Ray ray = MainCamera.ScreenPointToRay(screenPoint);
            float distance = (_planeZ - ray.origin.z) / ray.direction.z;
            return ray.GetPoint(distance);
        }

        private static Vector2 WorldToScreen(Vector3 worldPoint) => MainCamera.WorldToScreenPoint(worldPoint);

        private static Vector2 ClampToView(Vector2 point)
        {
            Rect view = View;
            return new Vector2(Mathf.Clamp(point.x, view.xMin, view.xMax), Mathf.Clamp(point.y, view.yMin, view.yMax));
        }

        // Native resolution keeps the default zoom's pixel density.
        private void ComputeOutput(out Rect viewport, out int width, out int height, out bool clamped)
        {
            Rect view = View;
            // Follows the mouse while placing.
            Rect selection = _selection != SelectionState.None ? SelectionRect : view;
            viewport = new Rect(
                (selection.x - view.x) / view.width,
                (selection.y - view.y) / view.height,
                selection.width / view.width,
                selection.height / view.height);

            float scale = Settings.NativeResolution ? _zoom : 1f;
            width = Mathf.Max(1, Mathf.RoundToInt(selection.width * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(selection.height * scale));

            int max = MaxTextureSize;
            clamped = width > max || height > max;
            if (clamped)
            {
                float k = (float)max / Mathf.Max(width, height);
                width = Mathf.Clamp(Mathf.FloorToInt(width * k), 1, max);
                height = Mathf.Clamp(Mathf.FloorToInt(height * k), 1, max);
            }
        }

        private static int MaxTextureSize => Mathf.Min(SystemInfo.maxTextureSize, 16384);

        #endregion

        #region Capture

        private void StartCapture(Output output)
        {
            if (!_busy)
            {
                StartCoroutine(Capture(output));
            }
        }

        private IEnumerator Capture(Output output)
        {
            _busy = true;
            string path = null;

            if (output == Output.File)
            {
                SetStatus(Strings.ChoosingFile, Color.white);
                string directory = Directory.Exists(Settings.LastSaveDirectory)
                    ? Settings.LastSaveDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                SaveDialog dialog = Os.ShowSaveDialog(Strings.SaveDialogTitle, directory, DefaultFileName());
                while (!dialog.IsDone)
                {
                    yield return null;
                }
                // Ignore the mode key pressed over the dialog.
                PrintScreenHook.ConsumePresses();

                if (!Active)
                {
                    yield break;
                }
                if (dialog.Error != null)
                {
                    Fail(dialog.Error);
                    _busy = false;
                    yield break;
                }
                if (dialog.Result == null)
                {
                    SetStatus(Strings.Cancelled, Color.white);
                    _busy = false;
                    yield break;
                }
                path = dialog.Result;
            }

            SetStatus(Strings.Rendering, Color.white);
            // Let the status show, then render after the frame.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (Active)
            {
                try
                {
                    RenderAndDeliver(output, path);
                }
                catch (Exception e)
                {
                    Fail(e.Message);
                    BetterScreenShot.Instance.LogError(e);
                }
            }
            _busy = false;
        }

        private void RenderAndDeliver(Output output, string path)
        {
            ComputeOutput(out Rect viewport, out int width, out int height, out _);
            Texture2D image = RegionRenderer.Render(MainCamera, viewport, width, height);
            try
            {
                byte[] png = image.EncodeToPNG();
                if (output == Output.File)
                {
                    File.WriteAllBytes(path, png);
                    Settings.LastSaveDirectory = Path.GetDirectoryName(path);
                    SetStatus(Strings.Saved(path), OkColor);
                }
                else
                {
                    Os.CopyImage(image, png);
                    SetStatus($"{Strings.Copied} ({width}×{height})", OkColor);
                }
                BetterScreenShot.Instance.Log($"Captured {width}x{height} at zoom {_zoom:0.00} to {(output == Output.File ? path : "clipboard")}");
            }
            finally
            {
                Destroy(image);
            }
        }

        // Screen, 1x and native renders of the same frame, plus a camera report.
        private IEnumerator SaveDiagnostics()
        {
            _hideUi = true;
            yield return new WaitForEndOfFrame();

            string directory = Path.Combine(
                Path.GetDirectoryName(typeof(ScreenshotMode).Assembly.Location),
                "Diagnostics",
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Texture2D screen = null;
            try
            {
                Directory.CreateDirectory(directory);
                screen = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory, "screen.png"), screen.EncodeToPNG());

                SaveViewRender(Path.Combine(directory, "render_1x.png"), 1f);
                if (!Mathf.Approximately(_zoom, 1f))
                {
                    SaveViewRender(Path.Combine(directory, "render_native.png"), _zoom);
                }
                File.WriteAllText(Path.Combine(directory, "info.txt"), Diagnostics.Report(MainCamera, _zoom, _baseDistance));
                SetStatus(Strings.DiagnosticsSaved(directory), OkColor);
                BetterScreenShot.Instance.Log("Diagnostics saved to " + directory);
            }
            catch (Exception e)
            {
                Fail(e.Message);
                BetterScreenShot.Instance.LogError(e);
            }
            finally
            {
                if (screen != null)
                {
                    Destroy(screen);
                }
                _hideUi = false;
                _busy = false;
            }
        }

        private static void SaveViewRender(string path, float scale)
        {
            Rect view = View;
            float k = Mathf.Min(scale, MaxTextureSize / Mathf.Max(view.width, view.height));
            Texture2D image = RegionRenderer.Render(MainCamera, new Rect(0f, 0f, 1f, 1f),
                Mathf.RoundToInt(view.width * k), Mathf.RoundToInt(view.height * k));
            try
            {
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                Destroy(image);
            }
        }

        private static string DefaultFileName()
        {
            return $"HollowKnight_{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        }

        private void SetStatus(string text, Color color)
        {
            _status = text;
            _statusColor = color;
        }

        private void Fail(string message)
        {
            SetStatus(Strings.ErrorPrefix + message, ErrorColor);
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (!Active)
            {
                return;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (_hideUi)
            {
                return;
            }
            if (_styles == null)
            {
                _styles = new Styles();
            }
            GUI.depth = -1000;

            GUI.matrix = Matrix4x4.identity;
            DrawSelectionOverlay();

            GUI.matrix = Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1f));
            _mainPanel.Draw("BetterScreenShot", _styles, DrawMainPanel);
            _timePanel.Draw(Strings.TimeTitle, _styles, DrawTimePanel);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawMainPanel()
        {
            GUI.enabled = !_busy;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{Strings.Zoom}: ×{_zoom:0.00}", _styles.Label, GUILayout.Width(120f));
            GUILayout.BeginVertical();
            GUILayout.Space(10f);
            // Logarithmic slider.
            float logZoom = Mathf.Log(_zoom);
            float newLogZoom = GUILayout.HorizontalSlider(logZoom, Mathf.Log(MinZoom), Mathf.Log(MaxZoom));
            GUILayout.EndVertical();
            if (!Mathf.Approximately(newLogZoom, logZoom))
            {
                SetZoom(Mathf.Exp(newLogZoom), View.center);
            }
            if (GUILayout.Button("1×", _styles.Button, GUILayout.Width(48f)))
            {
                SetZoom(1f, View.center);
            }
            GUILayout.EndHorizontal();

            Settings.NativeResolution = GUILayout.Toggle(Settings.NativeResolution, " " + Strings.NativeResolution, _styles.Toggle);
            bool hideMasks = GUILayout.Toggle(_hideMasks, " " + Strings.HideMasks, _styles.Toggle);
            if (hideMasks != _hideMasks)
            {
                _hideMasks = hideMasks;
                ApplyMasks();
            }

            GUILayout.Space(6f);
            if (_selection != SelectionState.None)
            {
                Rect selection = SelectionRect;
                ComputeOutput(out _, out int width, out int height, out bool clamped);
                string info = Strings.Selection(Mathf.RoundToInt(selection.width), Mathf.RoundToInt(selection.height), width, height);
                if (clamped)
                {
                    info += " " + Strings.Clamped(MaxTextureSize);
                }
                GUILayout.Label(info, _styles.Label);
            }
            if (_selection == SelectionState.Done)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(Strings.CopyButton, _styles.Button))
                {
                    StartCapture(Output.Clipboard);
                }
                if (GUILayout.Button(Strings.SaveButton, _styles.Button))
                {
                    StartCapture(Output.File);
                }
                if (GUILayout.Button(Strings.ClearSelection, _styles.Button, GUILayout.Width(110f)))
                {
                    _selection = SelectionState.None;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(Strings.SelectHint, _styles.Hint);
                if (GUILayout.Button(Strings.SelectAll, _styles.Button))
                {
                    SelectAll();
                }
            }

            GUI.enabled = true;
            if (!string.IsNullOrEmpty(_status))
            {
                _styles.Status.normal.textColor = _statusColor;
                GUILayout.Label(_status, _styles.Status);
            }
            GUILayout.Space(4f);
            GUILayout.Label(Strings.ControlsHint, _styles.Hint);
        }

        private void DrawTimePanel()
        {
            GUI.enabled = !_busy;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_time.Playing ? "||" : "►", _styles.ActiveButton, GUILayout.Width(52f)))
            {
                _time.TogglePlay();
            }
            foreach (float speed in TimeControl.Speeds)
            {
                bool current = _time.Playing && Mathf.Approximately(_time.Speed, speed);
                if (GUILayout.Button($"{speed:0.##}×", current ? _styles.ActiveButton : _styles.Button, GUILayout.Width(70f)))
                {
                    _time.Play(speed);
                }
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(Strings.Step1, _styles.Button))
            {
                _time.Step(1);
            }
            if (GUILayout.Button(Strings.Step10, _styles.Button))
            {
                _time.Step(10);
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            string state = _time.Stepping ? Strings.Stepping : _time.Playing ? Strings.Playing(_time.Speed) : Strings.Paused;
            GUILayout.BeginHorizontal();
            GUILayout.Label(state, _styles.Label, GUILayout.Width(150f));
            GUILayout.Label(Strings.TimeHint, _styles.Hint);
            GUILayout.EndHorizontal();
        }

        private void DrawSelectionOverlay()
        {
            if (_selection == SelectionState.None)
            {
                return;
            }

            Rect view = ToGui(View);
            Rect sel = ToGui(SelectionRect);

            // Dim outside the selection, which may reach off screen.
            Rect inside = Rect.MinMaxRect(
                Mathf.Clamp(sel.xMin, view.xMin, view.xMax),
                Mathf.Clamp(sel.yMin, view.yMin, view.yMax),
                Mathf.Clamp(sel.xMax, view.xMin, view.xMax),
                Mathf.Clamp(sel.yMax, view.yMin, view.yMax));
            FillRect(Rect.MinMaxRect(view.xMin, view.yMin, view.xMax, inside.yMin), DimColor);
            FillRect(Rect.MinMaxRect(view.xMin, inside.yMax, view.xMax, view.yMax), DimColor);
            FillRect(Rect.MinMaxRect(view.xMin, inside.yMin, inside.xMin, inside.yMax), DimColor);
            FillRect(Rect.MinMaxRect(inside.xMax, inside.yMin, view.xMax, inside.yMax), DimColor);

            // Frame sits just outside the selected pixels.
            const float t = 2f;
            FillRect(new Rect(sel.xMin - t, sel.yMin - t, sel.width + 2 * t, t), Styles.Accent);
            FillRect(new Rect(sel.xMin - t, sel.yMax, sel.width + 2 * t, t), Styles.Accent);
            FillRect(new Rect(sel.xMin - t, sel.yMin, t, sel.height), Styles.Accent);
            FillRect(new Rect(sel.xMax, sel.yMin, t, sel.height), Styles.Accent);

            const float handle = 8f;
            foreach (Vector2 corner in new[] { ToGui(WorldToScreen(_cornerA)), ToGui(WorldToScreen(_cornerB)) })
            {
                FillRect(new Rect(corner.x - handle / 2, corner.y - handle / 2, handle, handle), Styles.Accent);
            }

            ComputeOutput(out _, out int width, out int height, out _);
            var badge = new GUIContent($"{width}×{height}");
            Vector2 size = _styles.Badge.CalcSize(badge);
            float y = inside.yMin - size.y - 6f;
            if (y < view.yMin)
            {
                y = inside.yMax + 6f;
            }
            y = Mathf.Min(y, view.yMax - size.y);
            GUI.Label(new Rect(inside.xMin, y, size.x, size.y), badge, _styles.Badge);
        }

        private static void FillRect(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private static Rect ToGui(Rect screenRect)
        {
            return new Rect(screenRect.x, Screen.height - screenRect.yMax, screenRect.width, screenRect.height);
        }

        private static Vector2 ToGui(Vector2 screenPoint)
        {
            return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        }

        #endregion
    }
}
