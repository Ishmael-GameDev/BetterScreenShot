using UnityEngine;

namespace BetterScreenShot
{
    internal sealed class Styles
    {
        public static readonly Color Accent = new Color(1f, 0.82f, 0.3f, 1f);

        public readonly Texture2D Background;
        public readonly GUIStyle Panel;
        public readonly GUIStyle Title;
        public readonly GUIStyle Label;
        public readonly GUIStyle Hint;
        public readonly GUIStyle Status;
        public readonly GUIStyle Button;
        public readonly GUIStyle ActiveButton;
        public readonly GUIStyle Toggle;
        public readonly GUIStyle Badge;

        public Styles()
        {
            Background = new Texture2D(1, 1);
            Background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.08f, 0.93f));
            Background.Apply();

            Panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            Panel.normal.background = Background;

            Title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            Title.normal.textColor = Accent;

            Label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            Label.normal.textColor = Color.white;

            Hint = new GUIStyle(Label) { fontSize = 14 };
            Hint.normal.textColor = new Color(0.72f, 0.72f, 0.76f);

            Status = new GUIStyle(Label) { fontSize = 15 };

            Button = new GUIStyle(GUI.skin.button) { fontSize = 16, padding = new RectOffset(10, 10, 7, 7) };

            ActiveButton = new GUIStyle(Button) { fontStyle = FontStyle.Bold };
            ActiveButton.normal.textColor = Accent;
            ActiveButton.hover.textColor = Accent;

            Toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
            Toggle.normal.textColor = Color.white;
            Toggle.onNormal.textColor = Color.white;

            Badge = new GUIStyle(GUI.skin.box) { fontSize = 15, padding = new RectOffset(6, 6, 3, 3) };
            Badge.normal.background = Background;
            Badge.normal.textColor = Accent;
        }

        public void Destroy() => Object.Destroy(Background);
    }
}
