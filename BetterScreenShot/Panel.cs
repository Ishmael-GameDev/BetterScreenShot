using System;
using UnityEngine;

namespace BetterScreenShot
{
    // Own drag state instead of GUI.Window, so clamping to the screen edges doesn't jitter. GUI units, origin top-left.
    internal sealed class Panel
    {
        private const float TitleHeight = 30f;
        private const float Padding = 14f;

        private readonly float _width;
        private Vector2 _position;
        private float _height = 120f;
        private bool _dragging;
        private Vector2 _dragOffset;

        public Panel(float width, Vector2 position)
        {
            _width = width;
            _position = position;
        }

        public Rect Rect => new Rect(_position.x, _position.y, _width, _height);

        public bool IsDragging => _dragging;

        public float Height => _height;

        public bool Contains(Vector2 guiPoint) => Rect.Contains(guiPoint);

        public void MoveTo(Vector2 position) => _position = position;

        public void HandleDrag(Vector2 mouse, Vector2 screen, bool pressed, bool held)
        {
            if (pressed && new Rect(_position.x, _position.y, _width, TitleHeight).Contains(mouse))
            {
                _dragging = true;
                _dragOffset = mouse - _position;
            }
            if (_dragging)
            {
                if (held)
                {
                    _position = mouse - _dragOffset;
                }
                else
                {
                    _dragging = false;
                }
            }

            _position.x = Mathf.Clamp(_position.x, 0f, Mathf.Max(0f, screen.x - _width));
            _position.y = Mathf.Clamp(_position.y, 0f, Mathf.Max(0f, screen.y - _height));
        }

        public void Draw(string title, Styles styles, Action content)
        {
            Rect rect = Rect;
            GUI.Box(rect, GUIContent.none, styles.Panel);
            GUI.Label(new Rect(rect.x + Padding, rect.y + 6f, _width - 2 * Padding, TitleHeight - 6f), title, styles.Title);

            GUILayout.BeginArea(new Rect(rect.x + Padding, rect.y + TitleHeight, _width - 2 * Padding, 4000f));
            GUILayout.BeginVertical();
            content();
            GUILayout.EndVertical();
            // Real height is known only after layout; used from the next frame.
            if (Event.current.type == EventType.Repaint)
            {
                _height = TitleHeight + GUILayoutUtility.GetLastRect().height + Padding;
            }
            GUILayout.EndArea();
        }
    }
}
