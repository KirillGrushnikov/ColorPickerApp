using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorPickerApp;

/// <summary>
/// Раскладка вкладок по колонкам главного окна, слева направо.
/// Колонка <see cref="MainIndex"/> - основная: пипетка + вкладка «Значения» + полоса цвета; её нельзя убрать,
/// «Значения» в ней всегда первая. Остальные колонки - дополнительные слева и справа от неё, количество не ограничено.
/// Вкладки из <see cref="Hidden"/> не показываются.
/// </summary>
public sealed class WindowLayout
{
    public const string Values = "values";
    public const string Oklch = "oklch";
    public const string Scale = "scale";
    public const string Contrast = "contrast";
    public const string History = "history";
    public const string Help = "help";

    /// <summary>Вкладки, которые можно переносить и скрывать (порядок - как в классическом окне).</summary>
    public static readonly string[] MovableTabs = { Oklch, Scale, Contrast, History, Help };

    /// <summary>Ширина окна только с основной колонкой.</summary>
    public const double MainWindowWidth = 920;

    /// <summary>Ширина одной дополнительной колонки и отступ до соседней.</summary>
    public const double ColumnWidth = 340;
    public const double ColumnSpacing = 16;

    /// <summary>Особые «колонки» для <see cref="Move"/>.</summary>
    public const int HiddenColumn = -1;
    public const int NewLeftColumn = -2;
    public const int NewRightColumn = -3;

    /// <summary>Все колонки слева направо, основная - под индексом <see cref="MainIndex"/>.</summary>
    public List<List<string>> Columns { get; } = new();
    public List<string> Hidden { get; } = new();
    public int MainIndex { get; private set; }

    public WindowLayout(IEnumerable<IEnumerable<string>>? columns, IEnumerable<string>? hidden, int mainIndex = 0)
    {
        if (columns is not null)
        {
            foreach (var column in columns)
                Columns.Add(column?.ToList() ?? new List<string>());
        }
        if (hidden is not null)
            Hidden.AddRange(hidden);
        MainIndex = mainIndex;
        Normalize();
    }

    /// <summary>«Классический»: все вкладки в основной колонке.</summary>
    public static WindowLayout Classic => new(new[] { MovableTabs }, null);

    /// <summary>«Дизайнер»: Контраст слева, Значения посередине, История справа.</summary>
    public static WindowLayout Designer => new(
        new[]
        {
            new[] { Contrast },
            new[] { Oklch, Scale, Help },
            new[] { History },
        },
        null,
        mainIndex: 1);

    public List<string> MainColumn => Columns[MainIndex];

    public int LeftColumnCount => MainIndex;

    public int RightColumnCount => Columns.Count - MainIndex - 1;

    public int ExtraColumnCount => Columns.Count - 1;

    public double WindowWidth => WidthFor(ExtraColumnCount);

    public static double WidthFor(int extraColumns) => MainWindowWidth + extraColumns * (ColumnWidth + ColumnSpacing);

    public WindowLayout Clone() => new(Columns, Hidden, MainIndex);

    public bool SameAs(WindowLayout other) =>
        MainIndex == other.MainIndex
        && Columns.Count == other.Columns.Count
        && Columns.Zip(other.Columns).All(p => p.First.SequenceEqual(p.Second))
        && Hidden.SequenceEqual(other.Hidden);

    /// <summary>
    /// Переносит вкладку. <paramref name="column"/>: индекс колонки в <see cref="Columns"/>, <see cref="HiddenColumn"/> - скрыть,
    /// <see cref="NewLeftColumn"/> / <see cref="NewRightColumn"/> - в новую колонку с краю окна.
    /// <paramref name="index"/> - позиция в списке назначения (как он выглядел до переноса), больше длины - в конец.
    /// </summary>
    public void Move(string tab, int column, int index)
    {
        if (tab == Values || !MovableTabs.Contains(tab))
            return;

        List<string>? target = column switch
        {
            HiddenColumn => Hidden,
            >= 0 when column < Columns.Count => Columns[column],
            _ => null,
        };

        // Перенос внутри того же списка: после удаления позиции правее сдвигаются на одну
        if (target is not null)
        {
            var oldIndex = target.IndexOf(tab);
            if (oldIndex >= 0 && oldIndex < index)
                index--;
        }

        foreach (var list in Columns)
            list.Remove(tab);
        Hidden.Remove(tab);

        if (target is null)
        {
            target = new List<string>();
            if (column == NewLeftColumn)
            {
                Columns.Insert(0, target);
                MainIndex++;
            }
            else
            {
                Columns.Add(target);
            }
        }

        target.Insert(Math.Clamp(index, 0, target.Count), tab);
        Normalize();
    }

    /// <summary>
    /// Чинит раскладку из файла настроек: убирает неизвестные и повторные вкладки, пустые доп. колонки;
    /// вкладки, которых нигде нет (например, появились в новой версии), добавляет в основную колонку.
    /// </summary>
    private void Normalize()
    {
        if (Columns.Count == 0)
            Columns.Add(new List<string>());
        MainIndex = Math.Clamp(MainIndex, 0, Columns.Count - 1);

        var seen = new HashSet<string>();
        foreach (var list in Columns.Append(Hidden))
        {
            for (var i = 0; i < list.Count; i++)
            {
                var tab = list[i];
                if (!MovableTabs.Contains(tab) || !seen.Add(tab))
                    list.RemoveAt(i--);
            }
        }

        for (var i = Columns.Count - 1; i >= 0; i--)
        {
            if (i == MainIndex || Columns[i].Count > 0)
                continue;
            Columns.RemoveAt(i);
            if (i < MainIndex)
                MainIndex--;
        }

        foreach (var tab in MovableTabs)
        {
            if (!seen.Contains(tab))
                MainColumn.Add(tab);
        }
    }

    public static string Title(string tab) => Loc.T("tab_" + tab);
}
