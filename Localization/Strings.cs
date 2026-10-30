using System.Collections.Generic;

namespace ColorPickerApp;

/// <summary>
/// Все строки интерфейса: ключ -> (русский, английский).
/// Чтобы добавить строку: новый ключ здесь + {local:Tr ключ} в XAML или Loc.T("ключ") в коде.
/// Подстановки {0}, {1} - для Loc.F.
/// </summary>
internal static class Strings
{
    public static readonly Dictionary<string, (string Ru, string En)> Table = new()
    {
        // --- Трей ---
        ["tray_open"] = ("Открыть", "Open"),
        ["tray_picker"] = ("Пипетка", "Eyedropper"),
        ["tray_quit"] = ("Выход", "Quit"),
        ["tray_picker_fail"] = ("⚠ Хоткей пипетки {0} не работает", "⚠ Eyedropper hotkey {0} is not working"),
        ["tray_open_fail"] = ("⚠ Хоткей окна {0} не работает", "⚠ Show-window hotkey {0} is not working"),

        // --- Диалог назначения хоткея ---
        ["hk_title"] = ("Назначить горячую клавишу", "Assign a hotkey"),
        ["hk_prompt"] = ("Нажмите комбинацию клавиш или кнопку мыши", "Press a key combination or a mouse button"),
        ["hk_confirm"] = ("Подтвердить", "Confirm"),
        ["hk_cancel"] = ("Отмена", "Cancel"),
        ["hk_hint"] = (
            "Клавишам нужен Ctrl или Alt (кроме F1-F24). Боковые кнопки мыши (Mouse4 - «назад», Mouse5 - «вперёд») можно и без модификаторов, среднюю - только с Ctrl или Alt. Для отмены просто закройте окно.",
            "Keys need Ctrl or Alt (except F1-F24). Side mouse buttons (Mouse4 - \"back\", Mouse5 - \"forward\") work with or without modifiers, the middle button only with Ctrl or Alt. To cancel, just close the window."),
        ["hk_err_press"] = ("Нажмите комбинацию клавиш", "Press a key combination"),
        ["hk_err_unsupported"] = ("Эта клавиша не поддерживается для глобального хоткея", "This key can't be used as a global hotkey"),
        ["hk_err_middle"] = (
            "Средняя кнопка мыши - только с Ctrl или Alt: иначе она перестанет работать в других программах",
            "The middle mouse button needs Ctrl or Alt, otherwise it would stop working in other apps"),
        ["hk_err_modifier"] = (
            "Добавьте Ctrl или Alt: иначе эта клавиша перестанет работать в других программах",
            "Add Ctrl or Alt, otherwise this key would stop working in other apps"),
        ["hk_err_same"] = ("Эта комбинация уже назначена на {0}", "This combination is already assigned to {0}"),
        ["hk_err_busy"] = ("Комбинация уже занята другой программой или Windows", "This combination is already taken by another app or Windows"),
        ["hk_other_open"] = ("открытие окна", "showing the window"),
        ["hk_other_picker"] = ("пипетку", "the eyedropper"),
        ["hk_other_default"] = ("другое действие", "another action"),
        ["hk_status_busy"] = ("⚠ Комбинация занята другой программой - хоткей не работает", "⚠ Taken by another app - the hotkey doesn't work"),
        ["hk_status_invalid"] = ("⚠ Клавиша не поддерживается - назначьте другую", "⚠ Unsupported key - assign another one"),
        ["hk_status_duplicate"] = ("⚠ Совпадает с другим хоткеем - назначьте другую", "⚠ Same as the other hotkey - assign another one"),
        ["hk_status_hookfailed"] = ("⚠ Не удалось включить перехват кнопок мыши", "⚠ Couldn't start capturing mouse buttons"),

        // --- Настройки ---
        ["set_reset"] = ("Сбросить все настройки по умолчанию", "Reset all settings to defaults"),
        ["set_group_general"] = ("Общие", "General"),
        ["set_autostart"] = ("Запускать вместе с Windows", "Start with Windows"),
        ["set_tray"] = ("Сворачивать в трей при закрытии", "Close to the tray"),
        ["set_topmost"] = ("Поверх всех окон", "Always on top"),
        ["set_remember"] = ("Запоминать последний цвет", "Remember the last color"),
        ["set_group_picker"] = ("Пипетка", "Eyedropper"),
        ["set_autocopy"] = ("Копировать цвет сразу после пипетки", "Copy the color right after picking"),
        ["set_restore"] = ("Разворачивать главное окно после пипетки", "Restore the main window after picking"),
        ["set_group_lang"] = ("Язык / Language", "Язык / Language"),
        ["set_lang_auto"] = ("Как в Windows", "Same as Windows"),
        ["set_lang_restart"] = ("Язык сменится после перезапуска программы", "The language changes after a restart"),
        ["set_restart"] = ("Перезапустить", "Restart"),
        ["set_group_hotkeys"] = ("Горячие клавиши", "Hotkeys"),
        ["set_hk_picker"] = ("Пипетка", "Eyedropper"),
        ["set_hk_open"] = ("Открыть окно", "Show window"),
        ["set_hk_tip"] = ("Нажмите, чтобы назначить клавиши или кнопку мыши", "Click to assign keys or a mouse button"),
        ["set_group_hex"] = ("HEX", "HEX"),
        ["set_hex_alpha"] = ("Альфа", "Alpha"),
        ["set_hex_alpha_tip"] = (
            "Где стоит альфа в 8-значном HEX: при копировании, в поле Hex и при вводе",
            "Where alpha goes in 8-digit HEX: when copying, in the Hex field and when typing"),
        ["set_hex_alpha_end"] = ("#RRGGBBAA - в конце (CSS, Figma)", "#RRGGBBAA - at the end (CSS, Figma)"),
        ["set_hex_alpha_start"] = ("#AARRGGBB - в начале (Android, XAML)", "#AARRGGBB - at the start (Android, XAML)"),
        ["set_hex_upper"] = ("Заглавные буквы (#FF0000)", "Uppercase (#FF0000)"),
        ["set_group_bg"] = ("Фон прозрачных областей", "Background for transparent areas"),
        ["set_bg_light"] = ("Светлый шахматный", "Light checkerboard"),
        ["set_bg_dark"] = ("Тёмный шахматный", "Dark checkerboard"),
        ["set_bg_custom"] = ("Сплошной свой цвет", "Solid custom color"),
        ["set_bg_pick_tip"] = ("Выбрать цвет фона", "Choose the background color"),
        ["set_bg_current"] = ("Текущий", "Current"),
        ["set_bg_current_tip"] = ("Взять цвет, выбранный в главном окне", "Use the color selected in the main window"),

        // --- Раскладка окна ---
        ["set_group_layout"] = ("Раскладка окна", "Window layout"),
        ["set_tab_general"] = ("Основные", "General"),
        ["layout_hidden_empty"] = ("Перетащите сюда, чтобы скрыть", "Drop here to hide"),
        ["layout_presets"] = ("Пресеты:", "Presets:"),
        ["layout_classic"] = ("Классический", "Classic"),
        ["layout_classic_tip"] = ("Все вкладки в основной колонке - как раньше", "All tabs in the main column, as before"),
        ["layout_designer"] = ("Дизайнер", "Designer"),
        ["layout_designer_tip"] = ("Контраст слева, Значения посередине, История справа", "Contrast on the left, Values in the middle, History on the right"),
        ["layout_width"] = ("Ширина окна: {0} px", "Window width: {0} px"),
        ["layout_too_wide"] = (
            "Окно шире экрана ({0} px, доступно {1} px) - уберите колонку или скройте вкладку.",
            "The window is wider than the screen ({0} px, {1} px available) - remove a column or hide a tab."),
        ["layout_main"] = ("Основная", "Main"),
        ["layout_column"] = ("Колонка {0}", "Column {0}"),
        ["layout_new_column"] = ("+ новая колонка", "+ new column"),
        ["layout_hidden"] = ("Скрыто", "Hidden"),
        ["layout_eyedropper"] = ("Пипетка", "Eyedropper"),
        ["layout_strip"] = ("Цвет и копирование", "Color and copy"),
        ["layout_locked_tip"] = ("«Значения» всегда в основной колонке", "Values always stays in the main column"),
        ["layout_chip_tip"] = ("Перетащите в другую колонку, в новую (слева или справа) или в «Скрыто»", "Drag to another column, a new one (left or right) or Hidden"),
        ["layout_hint"] = (
            "Перетаскивайте вкладки мышью; новые колонки можно добавлять слева и справа. Основная колонка (пипетка, «Значения», полоса цвета) всегда на месте, пустые колонки убираются сами.",
            "Drag tabs with the mouse; new columns can be added on the left and on the right. The main column (eyedropper, Values, color bar) always stays; empty columns are removed automatically."),

        // --- Главное окно ---
        ["main_copied"] = ("Скопировано", "Copied"),
        ["main_with_alpha"] = ("С alpha", "With alpha"),
        ["swatch_tip"] = (
            "ЛКМ: применить сохранённый\nПКМ: сохранить текущий\nСКМ: очистить",
            "Left click: apply saved\nRight click: save current\nMiddle click: clear"),
        ["harmony_single"] = ("Моно", "Mono"),
        ["harmony_complementary"] = ("Контраст", "Complementary"),
        ["harmony_triad"] = ("Триада", "Triad"),
        ["harmony_tetrad"] = ("Тетрада", "Tetrad"),
        ["harmony_analogous"] = ("Аналогия", "Analogous"),
        ["harmony_accent"] = ("Акцент аналогия", "Analogous + accent"),

        // --- Вкладки ---
        ["tab_values"] = ("Значения", "Values"),
        ["tab_oklch"] = ("OKLCH", "OKLCH"),
        ["tab_scale"] = ("Шкала", "Shades"),
        ["tab_contrast"] = ("Контраст", "Contrast"),
        ["tab_history"] = ("История", "History"),
        ["tab_help"] = ("Справка", "Help"),

        // --- OKLCH ---
        ["oklch_box_tip"] = ("Можно вставить значение из CSS. Enter - применить, Esc - отменить", "Paste a CSS value. Enter - apply, Esc - cancel"),
        ["oklch_ring_tip"] = (
            "Тон (H): клик или перетаскивание по кругу, колесо мыши - по 1°, с Shift - по 10°",
            "Hue (H): click or drag on the ring, mouse wheel - 1°, with Shift - 10°"),
        ["oklch_l"] = ("L  светлота, %", "L  lightness, %"),
        ["oklch_c"] = ("C  хрома", "C  chroma"),
        ["oklch_h"] = ("H  тон, °", "H  hue, °"),
        ["oklch_out_of_gamut"] = (
            "⚠ Такой цвет экран показать не может: показан и копируется ближайший (с меньшей хромой).",
            "⚠ The screen can't show this color: the nearest one (with lower chroma) is shown and copied."),
        ["oklch_hint"] = (
            "Тяните ползунки или круг, крутите колесо мыши над ними (с Shift - крупный шаг), стрелки - точная подстройка. Шкалы показывают, каким станет цвет; затемнённые участки - цвета вне sRGB.",
            "Drag the sliders or the ring, scroll over them (Shift - bigger step), arrows - fine tuning. The scales preview the result; dimmed parts are colors outside sRGB."),

        // --- Шкала ---
        ["scale_hint"] = (
            "Оттенки 50-950 от текущего цвета (в рамке - исходный). Клик - выбрать ступень.",
            "Shades 50-950 of the current color (the original is framed). Click a step to select it."),
        ["scale_pin"] = ("Закрепить", "Pin"),
        ["scale_pin_tip"] = (
            "Не перестраивать шкалу при смене цвета - удобно перебирать ступени и сравнивать",
            "Don't rebuild the scale when the color changes - handy for comparing steps"),
        ["scale_name"] = ("Имя", "Name"),
        ["scale_name_tip"] = ("Имя палитры в коде: --primary-500, primary: { 500: ... }", "Palette name in code: --primary-500, primary: { 500: ... }"),
        ["scale_values"] = ("Значения", "Values"),
        ["scale_export_css"] = ("CSS-переменные (:root)", "CSS variables (:root)"),
        ["scale_export_tw4"] = ("Tailwind 4 (@theme)", "Tailwind 4 (@theme)"),
        ["scale_export_tw3"] = ("Tailwind 3 / JS-объект", "Tailwind 3 / JS object"),
        ["scale_export_list"] = ("Список значений", "Plain list"),
        ["scale_copy"] = ("Копировать шкалу", "Copy shades"),
        ["scale_row_tip"] = ("Клик - сделать этот оттенок текущим цветом", "Click to make this shade the current color"),

        // --- Контраст ---
        ["contrast_preview_tip"] = (
            "Текст - текущий цвет (полупрозрачный сначала смешивается с фоном), фон всегда непрозрачный",
            "Text is the current color (a translucent one is blended with the background first); the background is always opaque"),
        ["contrast_large_sample"] = ("Крупный текст Aa", "Large text Aa"),
        ["contrast_normal_sample"] = ("Обычный текст 14 px - пример", "Normal 14 px text sample"),
        ["contrast_text"] = ("Текст", "Text"),
        ["contrast_bg"] = ("Фон", "Background"),
        ["contrast_swap"] = ("⇄ Поменять", "⇄ Swap"),
        ["contrast_swap_tip"] = ("Поменять местами цвет текста и фон", "Swap the text and background colors"),
        ["contrast_use_current"] = ("Текущий → фон", "Current → bg"),
        ["contrast_use_current_tip"] = (
            "Сделать текущий цвет фоном, затем выбрать цвет текста (например, пипеткой)",
            "Make the current color the background, then pick the text color (e.g. with the eyedropper)"),
        ["contrast_normal"] = ("Обычный текст", "Normal text"),
        ["contrast_large"] = ("Крупный текст", "Large text"),
        ["contrast_large_tip"] = ("От 24 px, или от 18.7 px жирным", "24 px and up, or 18.7 px bold"),
        ["contrast_ui"] = ("Элементы UI", "UI elements"),
        ["contrast_ui_tip"] = ("Иконки, рамки полей, графики, состояния кнопок", "Icons, input borders, charts, button states"),
        ["contrast_target_tip"] = ("Какой уровень должен пройти подобранный цвет", "The level the suggested color must pass"),
        ["contrast_target_large"] = ("Крупный AA - 3 : 1", "Large AA - 3 : 1"),
        ["contrast_suggest_text"] = ("Подобрать текст", "Fix text"),
        ["contrast_suggest_text_tip"] = (
            "Ближайший цвет текста, проходящий уровень: меняется только светлота, тон и насыщенность сохраняются",
            "The nearest text color that passes the level: only lightness changes, hue and saturation stay"),
        ["contrast_suggest_bg"] = ("Подобрать фон", "Fix background"),
        ["contrast_suggest_bg_tip"] = (
            "Ближайший цвет фона, с которым текущий текст проходит уровень",
            "The nearest background color with which the current text passes the level"),
        ["contrast_name_aa"] = ("AA (4.5 : 1)", "AA (4.5 : 1)"),
        ["contrast_name_aaa"] = ("AAA (7 : 1)", "AAA (7 : 1)"),
        ["contrast_name_large"] = ("AA для крупного текста (3 : 1)", "AA for large text (3 : 1)"),
        ["contrast_text_passes"] = ("Текст уже проходит {0}.", "The text already passes {0}."),
        ["contrast_pair_passes"] = ("Пара уже проходит {0}.", "The pair already passes {0}."),
        ["contrast_what_text"] = ("Текст", "Text"),
        ["contrast_what_bg"] = ("Фон", "Background"),
        ["contrast_result_ok"] = (
            "{0}: светлота {1:0.0}% → {2:0.0}% ({3}), контраст {4:0.00} : 1 - проходит {5}.",
            "{0}: lightness {1:0.0}% → {2:0.0}% ({3}), contrast {4:0.00} : 1 - passes {5}."),
        ["contrast_result_fail"] = (
            "{0}: {1} с этим цветом недостижим. Подставлен максимум - {2:0.00} : 1 ({3}).",
            "{0}: {1} can't be reached with this color. The maximum was used instead - {2:0.00} : 1 ({3})."),

        // --- История ---
        ["history_hint"] = (
            "Цвета из пипетки и скопированные. ЛКМ - применить, ПКМ - удалить.",
            "Colors from the eyedropper and copied ones. Left click - apply, right click - remove."),
        ["history_clear"] = ("Очистить", "Clear"),
        ["history_empty"] = ("История пока пуста.", "No colors yet."),
        ["history_alpha"] = ("{0}, альфа {1:0.00}", "{0}, alpha {1:0.00}"),

        // --- Пипетка ---
        ["eyedropper_hint"] = (
            "колесо - масштаб · стрелки - 1 px · ПКМ/Esc - отмена",
            "wheel - zoom · arrows - 1 px · right click/Esc - cancel"),
    };
}
