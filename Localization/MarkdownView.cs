using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace ColorPickerApp;

/// <summary>
/// Мини-рендерер Markdown для справки - без сторонних библиотек.
/// Поддерживает: # / ## / ### заголовки, абзацы, списки "- " / "* ", блоки ``` , **жирный**, `код`,
/// комментарии &lt;!-- --&gt; (не показываются). Таблицы и картинки намеренно не поддерживаются.
/// </summary>
public static class MarkdownView
{
    private static readonly FontFamily CodeFont = new("Consolas, Courier New");
    private static readonly IBrush CodeForeground = new ImmutableSolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
    private static readonly IBrush CodeBlockBackground = new ImmutableSolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

    /// <summary>Загружает справку на текущем языке из ресурсов (Assets/help.ru.md / help.en.md).</summary>
    public static string LoadHelp()
    {
        foreach (var language in new[] { Loc.Language, Loc.Russian })
        {
            try
            {
                var uri = new Uri($"avares://ColorPickerApp/Assets/help.{language}.md");
                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                // нет файла для этого языка - пробуем русский
            }
        }

        return "# Help\n\nhelp.md not found.";
    }

    /// <summary>Строит содержимое из Markdown в панель (старое содержимое удаляется).</summary>
    public static void Render(Panel panel, string markdown)
    {
        panel.Children.Clear();

        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var inCode = false;
        var inComment = false;

        void FlushParagraph()
        {
            if (paragraph.Length == 0)
                return;
            panel.Children.Add(CreateText(paragraph.ToString(), "helpText"));
            paragraph.Clear();
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();

            // Комментарии <!-- ... --> (в том числе многострочные)
            if (inComment)
            {
                if (trimmed.Contains("-->", StringComparison.Ordinal))
                    inComment = false;
                continue;
            }
            if (!inCode && trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                FlushParagraph();
                inComment = !trimmed.Contains("-->", StringComparison.Ordinal);
                continue;
            }

            // Блок кода
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    panel.Children.Add(CreateCodeBlock(code.ToString().TrimEnd('\n')));
                    code.Clear();
                }
                else
                {
                    FlushParagraph();
                }
                inCode = !inCode;
                continue;
            }
            if (inCode)
            {
                code.Append(line).Append('\n');
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                FlushParagraph();
                var level = 0;
                while (level < trimmed.Length && trimmed[level] == '#')
                    level++;
                panel.Children.Add(CreateHeading(trimmed.Substring(level).Trim(), level));
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                panel.Children.Add(CreateBullet(trimmed.Substring(2).Trim()));
                continue;
            }

            // Обычный текст: строки подряд склеиваются в один абзац
            if (paragraph.Length > 0)
                paragraph.Append(' ');
            paragraph.Append(trimmed);
        }

        FlushParagraph();
        if (inCode && code.Length > 0)
            panel.Children.Add(CreateCodeBlock(code.ToString().TrimEnd('\n')));
    }

    private static Control CreateHeading(string text, int level)
    {
        var block = CreateText(text, "helpTitle");
        block.FontSize = level switch
        {
            1 => 19,
            2 => 15.5,
            _ => 13.5,
        };
        block.Margin = level switch
        {
            1 => new Thickness(0, 0, 0, 2),
            2 => new Thickness(0, 12, 0, 0),
            _ => new Thickness(0, 6, 0, 0),
        };
        return block;
    }

    private static Control CreateBullet(string text)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*"), Margin = new Thickness(4, 0, 0, 0) };
        var dot = CreateText("•", "helpText");
        var body = CreateText(text, "helpText");
        Grid.SetColumn(body, 1);
        grid.Children.Add(dot);
        grid.Children.Add(body);
        return grid;
    }

    private static Control CreateCodeBlock(string text) => new Border
    {
        Background = CodeBlockBackground,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 4),
        Margin = new Thickness(0, 2),
        Child = new TextBlock
        {
            Text = text,
            FontFamily = CodeFont,
            Foreground = CodeForeground,
            TextWrapping = TextWrapping.Wrap,
        },
    };

    private static TextBlock CreateText(string markdownText, string className)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        block.Classes.Add(className);

        var inlines = new InlineCollection();
        foreach (var (text, bold, isCode) in ParseInline(markdownText))
        {
            var run = new Run(text);
            if (bold)
                run.FontWeight = FontWeight.SemiBold;
            if (isCode)
            {
                run.FontFamily = CodeFont;
                run.Foreground = CodeForeground;
            }
            inlines.Add(run);
        }

        block.Inlines = inlines;
        return block;
    }

    /// <summary>Разбивает строку на куски: обычный текст, **жирный**, `код`.</summary>
    private static List<(string Text, bool Bold, bool Code)> ParseInline(string text)
    {
        var parts = new List<(string, bool, bool)>();
        var current = new StringBuilder();
        var bold = false;

        void Flush(bool isCode = false)
        {
            if (current.Length == 0)
                return;
            parts.Add((current.ToString(), bold, isCode));
            current.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    current.Append(text, i + 1, end - i - 1);
                    Flush(isCode: true);
                    i = end;
                    continue;
                }
            }

            if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                Flush();
                bold = !bold;
                i++;
                continue;
            }

            current.Append(text[i]);
        }

        Flush();
        return parts;
    }
}
