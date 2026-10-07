using BetterScreenShot.Native;

namespace BetterScreenShot
{
    internal static class Strings
    {
        private static bool Ru => Language.Language.CurrentLanguage() == Language.LanguageCode.RU;

        private static string Mod => Os.ModifierName;

        public static string Zoom => Ru ? "Зум" : "Zoom";
        public static string NativeResolution => Ru ? "Исходное разрешение (компенсировать отдаление камеры)" : "Native resolution (compensate camera zoom)";
        public static string HideMasks => Ru ? "Убрать виньетку и визуальные маски" : "Remove the vignette and visual masks";
        public static string DiagnosticsSaved(string path) => (Ru ? "Диагностика сохранена: " : "Diagnostics saved: ") + path;
        public static string CopyButton => Ru ? $"В буфер обмена ({Mod}+C)" : $"Copy to clipboard ({Mod}+C)";
        public static string SaveButton => Ru ? $"Сохранить как… ({Mod}+S)" : $"Save as… ({Mod}+S)";
        public static string ClearSelection => Ru ? "Сбросить" : "Clear";
        public static string SelectAll => Ru ? $"Выделить весь экран ({Mod}+A)" : $"Select the whole screen ({Mod}+A)";
        public static string SelectHint => Ru ? "ЛКМ: поставьте две точки (или протяните мышью), чтобы выделить область" : "LMB: place two points (or drag) to select an area";
        public static string ControlsHint => Ru
            ? $"Колесо, +/−: зум    ПКМ/СКМ + тянуть, стрелки/WASD: двигать камеру    Backspace, ПКМ: сбросить выделение    Esc/{Os.ToggleKeyName}: выход    F9: диагностика"
            : $"Wheel, +/-: zoom    RMB/MMB drag, arrows/WASD: move camera    Backspace, RMB: clear selection    Esc/{Os.ToggleKeyName}: exit    F9: diagnostics";
        public static string SaveDialogTitle => Ru ? "Сохранить скриншот" : "Save screenshot";
        public static string Rendering => Ru ? "Рендер…" : "Rendering…";
        public static string ChoosingFile => Ru ? "Выбор файла…" : "Choosing a file…";
        public static string Copied => Ru ? "Скопировано в буфер обмена" : "Copied to the clipboard";
        public static string Cancelled => Ru ? "Отменено" : "Cancelled";
        public static string ErrorPrefix => Ru ? "Ошибка: " : "Error: ";

        public static string TimeTitle => Ru ? "Время" : "Time";
        public static string Paused => Ru ? "Пауза" : "Paused";
        public static string Stepping => Ru ? "Шаг…" : "Stepping…";
        public static string Playing(float speed) => (Ru ? "Идёт: " : "Playing: ") + $"{speed:0.##}×";
        public static string Step1 => Ru ? "►| 1 кадр" : "►| 1 frame";
        public static string Step10 => Ru ? "►►| 10 кадров" : "►►| 10 frames";
        public static string TimeHint => Ru
            ? "Пробел: пауза/пуск    «.»: +1 кадр    Shift+«.»: +10 кадров"
            : "Space: pause/play    '.': +1 frame    Shift+'.': +10 frames";

        public static string Selection(int w, int h, int outW, int outH) => Ru
            ? $"Область: {w}×{h} px   →   итоговый размер: {outW}×{outH} px"
            : $"Area: {w}×{h} px   →   output size: {outW}×{outH} px";

        public static string Clamped(int max) => Ru
            ? $"(уменьшено до предела текстуры {max} px)"
            : $"(scaled down to the {max} px texture limit)";

        public static string Saved(string path) => (Ru ? "Сохранено: " : "Saved: ") + path;
    }
}
