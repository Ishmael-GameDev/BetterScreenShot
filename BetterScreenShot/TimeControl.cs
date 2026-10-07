using UnityEngine;

namespace BetterScreenShot
{
    internal sealed class TimeControl
    {
        public static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 0.1f };

        private const float StepDelta = 1f / 60f;

        private int _stepFrames;
        private int _stepRequestFrame;

        public bool Playing { get; private set; }

        public float Speed { get; private set; } = 1f;

        public bool Stepping => _stepFrames > 0;

        public void Reset()
        {
            Playing = false;
            Speed = 1f;
            _stepFrames = 0;
            Apply();
        }

        public void TogglePlay()
        {
            _stepFrames = 0;
            Playing = !Playing;
            Apply();
        }

        public void Play(float speed)
        {
            _stepFrames = 0;
            Speed = speed;
            Playing = true;
            Apply();
        }

        public void Step(int frames)
        {
            Playing = false;
            _stepFrames = frames;
            _stepRequestFrame = Time.frameCount;
            Apply();
        }

        public void Update() => Apply();

        // Steps count down after rendering, so time never stops halfway through a frame.
        public void EndOfFrame()
        {
            if (_stepFrames > 0 && Time.frameCount > _stepRequestFrame)
            {
                _stepFrames--;
                Apply();
            }
        }

        public static void Release()
        {
            Time.captureDeltaTime = 0f;
            // Back to the game's own time scale.
            float timeScale = TimeController.SlowMotionTimeScale * TimeController.PauseTimeScale
                * TimeController.PlatformBackgroundTimeScale * TimeController.GenericTimeScale;
            Time.timeScale = timeScale < 0.01f ? 0f : timeScale;
        }

        // Every frame, since hit-stop keeps resetting it.
        private void Apply()
        {
            if (_stepFrames > 0)
            {
                Time.timeScale = 1f;
                Time.captureDeltaTime = StepDelta;
            }
            else
            {
                Time.captureDeltaTime = 0f;
                Time.timeScale = Playing ? Speed : 0f;
            }
        }
    }
}
