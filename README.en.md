# MarkdownExporter

English | [日本語](README.md)

MarkdownExporter is a Windows application that converts Markdown files to HTML and PDF.

## Requirements

- Windows 11 24H2 or later
- WebView2 Runtime (required for both HTML and PDF conversion)

## Usage

Run `mdexport.exe` to open the graphical interface.
Drop Markdown files onto the window to add them to the list, review the settings, and click Convert to create HTML and PDF files.

The installer offers an option to add MarkdownExporter to Explorer's Send to menu; it is selected by default.
Select multiple files and choose Send to → MarkdownExporter to open the GUI with the Markdown files already in the list.
Other files and folders are ignored.

Choose "Beside each source file" (the default) or "Specified folder" for the output location.
The first choice writes output beside each Markdown file; the second writes to a shared folder. Values saved in Default Settings become the initial settings on subsequent launches.

When output goes to a specified folder, local relative links may resolve to different files. If affected links are found, the GUI asks for confirmation before conversion.
If you continue, a warning remains on each affected file's result. Linked files outside the conversion set are not copied, and those links are not rewritten.
Links to successfully converted Markdown files point to the generated HTML or PDF.

MarkdownExporter uses WebView2 Runtime for conversion. If it is missing, the GUI offers to open Microsoft's download page. The CLI displays the download URL and exits with an error.
The ZIP does not bundle the Runtime. If needed, the installer installs it automatically; this requires an internet connection.

The installer selects "Add MarkdownExporter to the user PATH" by default.
With this option enabled, you can run `mdexport.exe` by name from a terminal. Clear the checkbox during installation if you do not need it.

### Page breaks

To start a new page at a chosen position in the PDF, put the following line on its own in the Markdown source:

```markdown
Text on the previous page

<!-- pagebreak -->

Text on the next page
```

The page break applies to PDF output and when printing the HTML. It is not shown when viewing the HTML on screen.

### Figures and preceding headings

When "Fit each figure on one page" is enabled for PDF output, you can also select "Keep heading with figure". It is off by default. When enabled, one heading immediately before an image or Mermaid diagram stays on the same page, and the figure is reduced to leave room for it. It does not apply when body text, a list, or an explicit page break separates the heading from the figure.

### Front matter

If a Markdown file begins with YAML front matter enclosed by `---` lines, its fields appear in a two-column table at the beginning of the document. Lists appear as bullet lists, nested mappings as tables, and multiline values retain their line breaks. Values are not executed as HTML.

```yaml
---
title: "Frontmatter"
retrieved_at: "2026-11-05T15:13:13.171000+09:00"
---
```

The front matter `title` does not set the HTML `<title>`. The first level-one heading in the body sets the HTML title; if none exists, the input filename is used. Invalid YAML front matter causes that file's conversion to fail.

### CLI

To convert without opening the GUI, pass `--run` and one or more `.md` or `.markdown` files. You can process multiple files together; quote paths that contain spaces.
With no conversion options, the CLI writes `<input-name>.html` and `<input-name>.pdf` beside each input file. CLI conversion options do not use defaults saved in the graphical interface.

The GUI option "Convert linked Markdown files" or CLI flag `--follow-markdown-links` recursively follows local relative Markdown links and converts the discovered files too.
Without it, only explicitly added files are converted. Links between successfully converted Markdown files point to HTML from HTML and to PDF from PDF.

```text
mdexport.exe --run <input.md> [another.markdown ...] [options]
```

| Option | Behavior and default |
| --- | --- |
| `--run` | Convert without opening the GUI. Required for command-line conversion. |
| `-o <folder>` / `--output <folder>` | Send all outputs to this folder. By default, each output is written beside its input. If you specify a relative path, it is resolved from the current working directory. |
| `--template <name-or-absolute-path>` | Use a template. The default is `default`. A name selects `%APPDATA%\MarkdownExporter\Template\<name>`; an absolute path selects that folder. |
| `--format <format>` | Choose `html`, `pdf`, or `both`; default: `both`. `html` and `pdf` produce only the named format. |
| `--embed-images` | Embed local images as Data URIs in HTML. By default, they are copied to the HTML asset folder. |
| `--orientation <orientation>` | Choose `portrait` (A4 portrait) or `landscape` (A4 landscape); default: `portrait`. |
| `--fit-figures-to-page` | Scale images and Mermaid diagrams down to the PDF printable area; off by default. |
| `--keep-heading-with-figure` | Keep the one heading immediately before a figure on the same PDF page. Requires `--fit-figures-to-page`; off by default. |
| `--follow-markdown-links` | Recursively convert locally linked Markdown files; off by default. |
| `--encoding <encoding>` | Choose `auto`, `utf-8`, `shift-jis`, or `euc-jp`; default: `auto`. UTF-8 is tried first, then Shift JIS/EUC-JP detection. Detection failure is an error. The choice applies to all inputs. |
| `--overwrite` | Replace existing HTML/PDF outputs; off by default. |
| `--help` / `-h` | Show command-line help. |

Examples from the distribution folder in PowerShell:

```powershell
.\mdexport.exe --run .\report.md
.\mdexport.exe --run .\chapter1.md ".\draft chapter.md" --format html -o "C:\Output Files" --template custom
.\mdexport.exe --run .\report.md --format pdf --orientation landscape --fit-figures-to-page
.\mdexport.exe --run .\legacy.md --encoding shift-jis --embed-images --overwrite
.\mdexport.exe --run .\index.md --format both --follow-markdown-links --output .\out
.\mdexport.exe --help
```

Without `--overwrite`, an input whose output already exists fails, while other inputs continue.
When names collide in one output folder, sequential suffixes such as `intro.html` and `intro-2.html` keep them distinct.
Original file stems are reserved first, so an existing source named `intro-2.md` keeps its stem. Parent folder names are never added to output names.
Exit codes are `0` for success or help, `1` for conversion failure, and `2` for invalid arguments.

With `--output`, the CLI converts without prompting and prints a warning to standard error if local relative links may resolve to different files. A warning alone does not change the exit code.

## Templates

On the first launch, the application copies the default template files to `%APPDATA%\MarkdownExporter\Template\default`.
You can add templates by creating folders under `%APPDATA%\MarkdownExporter\Template` and placing HTML and CSS files inside them.
A template must contain `index.html` and `style.css`. Place `{{content}}` and `{{style}}` in `index.html`; `{{title}}` is optional. Put additional images and fonts in `assets/`. Custom JavaScript is not supported.

## Silent installation options

The following options are available for silent installation.

| Option | Description |
| --- | --- |
| `/MERGETASKS="!sendto"` | Do not add MarkdownExporter to the Send to menu. |
| `/MERGETASKS="!addtopath"` | Do not add the MarkdownExporter installation folder to the user PATH. |

## Development

```powershell
./tools/prepare-qpdf.ps1 -DestinationDir ./artifacts/qpdf
dotnet build MarkdownExporter.slnx -m:1
dotnet test MarkdownExporter.slnx -m:1
dotnet publish MarkdownExporter/MarkdownExporter.csproj -c Release -r win-x64 -m:1
```

`MarkdownExporter.Tests` uses NUnit to test logic such as CLI parsing, character encoding, preflight checks, assets and templates, Markdown and PDF links, and settings persistence. These tests do not cover WebView2 rendering or graphical interface interactions.

Include `mdexport.exe`, `Template/`, `WebAssets/`, `Licence/`, and `Tools/qpdf/` in a published distribution. Fetch qpdf with the script above before building. Third-party notices, the NuGet dependency inventory, and license texts are collected in `Licence/`. `Licence/KaTeX-LICENSE.txt` contains the SIL Open Font License 1.1 and copyright notices for the KaTeX fonts; it is also copied alongside fonts in HTML output containing math or Mermaid. To rebuild `WebAssets`, run `npm ci` and `npm run build` in `tools/web-assets/`. The ZIP and installer include these files.
