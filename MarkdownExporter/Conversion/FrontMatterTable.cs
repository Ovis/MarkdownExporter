using System.Net;
using System.Text;
using MarkdownExporter.Localization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MarkdownExporter.Conversion;

/// <summary>YAML Frontmatterの項目を本文先頭に表示する表へ変換する。</summary>
internal static class FrontMatterTable
{
    private const int MaximumDepth = 32;

    /// <summary>最上位の項目を表にし、入れ子の値も読める形に整える。</summary>
    /// <param name="yaml">区切り行を除いたFrontmatterの本文。</param>
    /// <returns>HTML表。項目がない場合は空文字列。</returns>
    public static string Render(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return string.Empty;

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException(LanguageManager.Format("FrontMatterInvalid", ex.Message), ex);
        }

        if (stream.Documents.Count == 0)
            return string.Empty;
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode mapping)
            throw new InvalidDataException(LanguageManager.Text("FrontMatterMappingRequired"));
        if (mapping.Children.Count == 0)
            return string.Empty;

        return RenderMapping(mapping, 0);
    }

    /// <summary>項目名を左列、値を右列に置いた表を作る。</summary>
    private static string RenderMapping(YamlMappingNode mapping, int depth)
    {
        EnsureDepth(depth);
        var html = new StringBuilder("<table class=\"frontmatter\"><tbody>");
        foreach (var (key, value) in mapping.Children)
        {
            html.Append("<tr><th>");
            html.Append(RenderNode(key, depth + 1));
            html.Append("</th><td>");
            html.Append(RenderNode(value, depth + 1));
            html.Append("</td></tr>");
        }

        return html.Append("</tbody></table>").ToString();
    }

    /// <summary>YAMLの値の種類に応じて、文字列、箇条書き、表を選ぶ。</summary>
    private static string RenderNode(YamlNode node, int depth)
    {
        EnsureDepth(depth);
        return node switch
        {
            YamlScalarNode scalar => RenderScalar(scalar),
            YamlSequenceNode sequence => RenderSequence(sequence, depth),
            YamlMappingNode mapping => RenderMapping(mapping, depth),
            _ => throw new InvalidDataException(LanguageManager.Text("FrontMatterUnsupportedValue"))
        };
    }

    /// <summary>配列の順序を保って箇条書きにする。</summary>
    private static string RenderSequence(YamlSequenceNode sequence, int depth)
    {
        var html = new StringBuilder("<ul class=\"frontmatter-list\">");
        foreach (var child in sequence.Children)
        {
            html.Append("<li>");
            html.Append(RenderNode(child, depth + 1));
            html.Append("</li>");
        }

        return html.Append("</ul>").ToString();
    }

    /// <summary>値をHTMLとして解釈させず、元の引用符の有無を表へ反映する。</summary>
    private static string RenderScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        value = scalar.Style switch
        {
            ScalarStyle.DoubleQuoted => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            ScalarStyle.SingleQuoted => "'" + value.Replace("'", "''") + "'",
            _ => value
        };

        return WebUtility.HtmlEncode(value);
    }

    /// <summary>循環参照を含む深い構造で無限再帰しないようにする。</summary>
    private static void EnsureDepth(int depth)
    {
        if (depth > MaximumDepth)
            throw new InvalidDataException(LanguageManager.Text("FrontMatterTooDeep"));
    }
}
