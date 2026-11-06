# MarkdownExporter

[English](README.en.md) | 日本語

MarkdownファイルをHTMLとPDFに変換するWindowsアプリです。

## 動作環境

- Windows 11 24H2以降
- WebView2 Runtime（HTMLとPDFのどちらにも必要）

## 使い方

`mdexport.exe` を起動するとGUIが開きます。
Markdownファイルを画面へドロップすると一覧に追加され、設定を確認してから「変換」を押すことでHTML、PDFに変換できます。

インストーラ版では、エクスプローラーの「送る」にMarkdownExporterを追加するオプションが選べます（既定でチェックが入っています）。
複数のファイルを選択して「送る」→「MarkdownExporter」を選ぶと、一覧に Markdownファイルが初期登録された状態で起動します。
それ以外のファイルやフォルダーを送った場合は無視されます。

出力先は「元ファイルと同じ場所」（既定）と「指定したフォルダー」から選べます。
前者は各Markdownファイルと同じフォルダーに出力し、後者は指定した共通フォルダーに出力します。「既定の設定」で設定した場合、以降は設定した値を起動時の初期設定として利用します。

指定したフォルダーへ出力する際、元ファイルを基準とするローカル相対リンクの参照先が変わる可能性があれば、変換前に確認します。
続行した場合も、該当ファイルの結果に警告を残します。変換対象外のリンク先ファイルは自動コピーせず、該当リンクも書き換えません。
変換できたMarkdownへのリンクは、生成したHTMLまたはPDFを指すように変換します。

このプログラムでは変換のためにWebView2 Runtimeを利用します。インストールされていない場合、Microsoftのダウンロードページを案内します。CLIの場合はダウンロードページのURLを案内したうえでエラーを表示して終了します。
ZIP版はRuntimeを同梱していません。インストーラ版はWebView2 Runtimeが未導入の場合、自動的にRuntimeをインストールします（ネットワーク接続が必要です）。

インストーラは「MarkdownExporterをユーザーのPATHに追加する」のオプションを既定でオンにしています。
この設定がオンの場合、ターミナルから `mdexport.exe` を名前だけで実行できます。不要ならインストール時にチェックを外してください。

### 改ページ

PDFの任意の位置で改ページするには、Markdown本文に次の行を単独で記述します。

```markdown
前のページの本文

<!-- pagebreak -->

次のページの本文
```

改ページはPDFとHTMLの印刷時に適用されます。HTMLを画面で表示するときは改ページ位置を表示しません。

### 図と直前の見出し

PDFで「1ページに収める」をオンにした場合、「見出しも図と同じページに出力」を追加で選べます。既定はオフです。オンにすると、画像またはMermaid図の直前にある見出しを1つだけ図と一緒に配置し、見出しの分だけ図を縮小します。見出しと図の間に本文・リスト・改ページ指定がある場合は適用しません。

### Frontmatter

Markdownの先頭に `---` で囲んだYAML Frontmatterがある場合、項目を本文の先頭に2列の表として表示します。配列は箇条書き、入れ子の項目は表、複数行の値は改行を保って表示します。値はHTMLとして実行しません。

```yaml
---
title: "Frontmatter"
retrieved_at: "2026-11-05T15:13:13.171000+09:00"
---
```

Frontmatterの `title` はHTMLの `<title>` に使いません。HTMLのタイトルは本文の最初のレベル1見出しから決め、見出しがなければ入力ファイル名を使います。FrontmatterのYAMLが不正な場合は、そのファイルの変換に失敗します。

### CLI

GUIを開かずに変換するには `--run` と1個以上の `.md` または `.markdown` ファイルを指定します。複数ファイルをまとめて指定でき、パスに空白がある場合は引用符で囲みます。
オプションを省略すると、各入力ファイルの隣に同名の `.html` と `.pdf` を作成します。CLIの変換オプションはGUIに保存した既定値を参照しません。

GUIの「リンク先のMarkdownも変換」またはCLIの `--follow-markdown-links` を指定すると、ローカルの相対Markdownリンクを再帰的にたどり、見つかったファイルも変換します。
省略時は明示的に追加したファイルだけを変換します。変換できたMarkdown同士のリンクは、HTMLからはHTML、PDFからはPDFへ向けます。

```text
mdexport.exe --run <入力.md> [別の入力.markdown ...] [オプション]
```

| オプション | 説明・省略時の動作 |
| --- | --- |
| `--run` | GUIを開かずに変換します。CLI変換では必須です。 |
| `-o <フォルダー>` / `--output <フォルダー>` | すべての入力の出力先を指定します。省略時は各入力ファイルと同じフォルダーです。相対パスを指定した場合は現在の作業フォルダーから解決します。 |
| `--template <名前または絶対パス>` | 使用するテンプレート。省略時は `default`。名前を指定すると `%APPDATA%\MarkdownExporter\Template\<名前>`、絶対パスを指定するとそのフォルダーを読み込みます。 |
| `--format <形式>` | `html`、`pdf`、`both` から選びます。省略時は `both`。`html` と `pdf` はそれぞれ指定した形式のみ出力します。 |
| `--embed-images` | HTMLのローカル画像をData URIで埋め込みます。省略時はHTMLの素材フォルダーへコピーします。 |
| `--orientation <向き>` | PDFの用紙方向。`portrait`（A4縦）または `landscape`（A4横）を指定します。省略時は `portrait` です。 |
| `--margin-top <mm>` / `--margin-bottom <mm>` / `--margin-left <mm>` / `--margin-right <mm>` | PDFの上・下・左・右の余白を個別にmm単位で指定します。各方向の既定値は `15`。0以上の数値を指定できます。 |
| `--fit-figures-to-page` | PDFの画像・Mermaid図を印刷領域に収まるよう縮小します。省略時は適用しません。 |
| `--keep-heading-with-figure` | 図の直前にある見出し1つを同じPDFページに配置します。`--fit-figures-to-page` と併用します。省略時は適用しません。 |
| `--follow-markdown-links` | ローカルのMarkdownリンクを再帰的にたどり、リンク先も変換します。省略時は指定ファイルのみ変換します。 |
| `--encoding <文字コード>` | `auto`、`utf-8`、`shift-jis`、`euc-jp` から選びます。省略時は `auto`。UTF-8を優先し、それ以外はShift JIS／EUC-JPを自動判定します。判定できない場合はエラーになります。指定値はすべての入力に適用します。 |
| `--overwrite` | 既存のHTML・PDFを上書きします。省略時は上書きしません。 |
| `--help` / `-h` | コマンドのヘルプを表示します。 |

PowerShellで配布フォルダーから実行する例:

```powershell
.\mdexport.exe --run .\report.md
.\mdexport.exe --run .\chapter1.md ".\draft chapter.md" --format html -o "C:\Output Files" --template custom
.\mdexport.exe --run .\report.md --format pdf --orientation landscape --fit-figures-to-page
.\mdexport.exe --run .\report.md --format pdf --margin-top 10 --margin-bottom 20 --margin-left 15 --margin-right 15
.\mdexport.exe --run .\legacy.md --encoding shift-jis --embed-images --overwrite
.\mdexport.exe --run .\index.md --format both --follow-markdown-links --output .\out
.\mdexport.exe --help
```

`--overwrite` を指定せずに既存の出力ファイルがある場合、その入力は失敗し、ほかの入力の処理は続きます。
同じ出力先でファイル名が重なるときは `intro.html`、`intro-2.html` のように連番を付けます。
元のファイル名を先に確保するため、元から `intro-2.md` があればその名前を優先します。親フォルダー名は出力名に含めません。
終了コードは成功・ヘルプ表示が `0`、変換失敗が `1`、引数の誤りが `2` です。

CLIで `--output` を指定し、ローカル相対リンクの参照先が変わる可能性がある場合は、確認せずに変換して標準エラー出力に警告を表示します。警告だけでは終了コードは変わりません。

## テンプレート

初回起動時にデフォルトテンプレートファイルを `%APPDATA%\MarkdownExporter\Template\default` へコピーします。
テンプレートは必要に応じて追加が可能です。`%APPDATA%\MarkdownExporter\Template`にフォルダーを作り、その中にHTMLファイルおよびCSSファイルを追加します。
テンプレートは `index.html` と `style.css` が必須です。`index.html` には `{{content}}` と `{{style}}` を入れ、必要なら `{{title}}` を使えます。追加の画像とフォントは `assets/` に置きます。カスタムJavaScriptは使えません。

## サイレントインストールのオプション
サイレントインストールでは以下のオプションが利用可能です。

| オプション | 説明 |
| --- | --- |
|`/MERGETASKS="!sendto"` | SendToへの登録を無効にできます。|
|`/MERGETASKS="!addtopath"` |ユーザーのPATHにMarkdownExporterのインストールパスを追加しません。|

## 開発

```powershell
./tools/prepare-qpdf.ps1 -DestinationDir ./artifacts/qpdf
dotnet build MarkdownExporter.slnx -m:1
dotnet test MarkdownExporter.slnx -m:1
dotnet publish MarkdownExporter/MarkdownExporter.csproj -c Release -r win-x64 -m:1
```

`MarkdownExporter.Tests` はNUnitでCLI解析、文字コード、事前検査、素材・テンプレート、Markdown/PDFリンク、設定保存などのロジックを検証します。WebView2の描画と画面操作はこのテストには含みません。

発行物には `mdexport.exe` と `Template/`、`WebAssets/`、`Licence/`、`Tools/qpdf/` を含めてください。qpdfはビルド前に上記スクリプトで取得します。サードパーティーの通知、NuGet依存の一覧とライセンスは `Licence/` にまとめています。KaTeXフォントのSIL Open Font License 1.1と著作権表示は `Licence/KaTeX-LICENSE.txt` に収録し、数式・Mermaidを含むHTMLの素材にもコピーします。`WebAssets` は `tools/web-assets/` で `npm ci` と `npm run build` により再生成できます。配布用ZIPとインストーラはこれらのファイルを同梱します。
