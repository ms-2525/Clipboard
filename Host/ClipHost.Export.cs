using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Clip.Core;
using Clip.Interop;
using Microsoft.Win32;

namespace Clip.Host
{
    /// <summary>
    /// The "Export" path. Conversion, the offered name and every refusal live in <see cref="ClipExport"/>;
    /// this file owns only the OS dialog and the one write to the path the user confirmed.
    /// </summary>
    internal sealed partial class ClipHost
    {
        /// <summary>
        /// Bridge method "export". Runs on the UI thread (the dispatcher's nested message loop keeps the page
        /// alive while the dialog is open), so no lock and no async around ShowDialog.
        /// </summary>
        object Export(JsonElement p)
        {
            // The editor's "Open in app": the same bytes Export would write, but to a temp file the OS opens.
            if (Bool(p, "launch")) return ExportLaunch(_store, _dbPath, Long(p, "id"));
            return Export(_store, _dbPath, Long(p, "id"), String(p, "format") ?? "auto", AskPath, _modals, _show);
        }

        /// <summary>
        /// One export with the shell and its dialog injected, so a harness can drive save and cancel without a
        /// window. The popup hides itself whenever it loses activation; that is exactly what opening the dialog
        /// does, so the gate is held across it and the shell is brought back (visible and focused) before this
        /// returns — and therefore before the reply that carries the page's toast.
        /// </summary>
        internal static object Export(ClipboardStore store, string dbPath, long id, string requested,
            Func<ClipExportResult, string, string> askPath, ModalGate modals, Action show)
        {
            var stored = store.GetImage(id);
            if (stored == null) throw new ArgumentException("no clip with id " + id);
            var formats = store.GetFormats(id).Select(f => f.fmt).ToList();
            var item = new ClipboardItem
            {
                Id = id,
                Kind = stored.Kind,
                Text = store.GetText(id),
                Image = stored.Image,
                CapturedAt = CapturedAtOf(dbPath, id)
            };

            // Refuses a format that contradicts the kind (png for text, txt for a spreadsheet) before any dialog.
            var planned = ClipExport.Prepare(item, formats, requested);
            if (!planned.Ok) throw new InvalidOperationException(planned.Error);

            modals.Enter();
            try
            {
                string chosen = askPath(planned,
                    ClipExport.SuggestedName(item.Text, planned.Extension, CapturedAt(item.CapturedAt)));
                if (chosen == null) return new { saved = false, cancelled = true };

                // The user can type another extension in the box; it still has to match the clip's kind.
                var final = ClipExport.Prepare(item, formats, Path.GetExtension(chosen));
                if (!final.Ok) throw new InvalidOperationException(final.Error);

                try
                {
                    ClipExport.Write(chosen, final.Bytes);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // PathTooLongException is an IOException; the page toasts this sentence.
                    throw new InvalidOperationException("Could not save the clip: " + ex.Message);
                }
                return new { saved = true, path = chosen };
            }
            finally
            {
                // Save, cancel or a failed write: the dialog took activation, so the shell comes back first.
                modals.Leave();
                show();
            }
        }

        /// <summary>The real Save dialog, owned by the shell so the owner keeps it in front of the app.</summary>
        string AskPath(ClipExportResult planned, string suggested)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save clip",
                FileName = suggested,
                DefaultExt = planned.Extension,
                AddExtension = true,
                OverwritePrompt = true,
                ValidateNames = true,
                CheckPathExists = true,
                Filter = Filter(planned.Kind)
            };
            return dlg.ShowDialog(Window.GetWindow(_web)) == true ? dlg.FileName : null;
        }

        static string Filter(ClipExportKind kind)
        {
            switch (kind)
            {
                case ClipExportKind.Spreadsheet: return "CSV (*.csv)|*.csv";
                case ClipExportKind.Image: return "PNG image (*.png)|*.png";
                default: return "Text (*.txt)|*.txt|Markdown (*.md)|*.md";
            }
        }

        /// <summary>
        /// The editor's "Open in app" (bridge "export" with launch:true). No dialog: the clip is written to a
        /// temp file and handed to the OS default app. Rich text opens as Markdown (formatting is
        /// the point); every other kind reuses the exact Save-as bytes, so a file list is refused the same way.
        /// </summary>
        internal static object ExportLaunch(ClipboardStore store, string dbPath, long id)
        {
            var (path, ext) = TempClipFile(store, dbPath, id);
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("No app opens " + ext + " files: " + ex.Message);
            }
            return new { saved = false, launched = true, path };
        }

        /// <summary>The clip's Save-as bytes written to a temp file for the default app to open.</summary>
        static (string path, string ext) TempClipFile(ClipboardStore store, string dbPath, long id)
        {
            var stored = store.GetImage(id);
            if (stored == null) throw new ArgumentException("no clip with id " + id);
            var formats = store.GetFormats(id).Select(f => f.fmt).ToList();
            var rich = store.GetRich(id);
            var item = new ClipboardItem
            {
                Id = id,
                Kind = stored.Kind,
                Text = store.GetText(id),
                Image = stored.Image,
                CapturedAt = CapturedAtOf(dbPath, id)
            };

            string ext = null;
            byte[] bytes = null;
            string fragment = null;
            if (!string.IsNullOrEmpty(rich?.Html))
            {
                try { fragment = HtmlFragment.Extract(rich.Html); }
                catch { fragment = null; } // not a CF_HTML envelope: plain text below still opens
            }
            if (!string.IsNullOrEmpty(fragment))
            {
                // Rich text opens as Markdown in the default .md app: headings, lists, tables,
                // links and emphasis survive as real Markdown; colours and sizes ride along as
                // inline HTML spans. A fragment that converts to nothing falls back to HTML below.
                string markdown = null;
                try { markdown = Clip.Core.HtmlToMarkdown.Convert(fragment); }
                catch { markdown = null; } // a hostile fragment must not break Open in app
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    ext = ".md";
                    bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(markdown);
                }
            }
            if (ext == null)
            {
                if (!string.IsNullOrEmpty(fragment))
                {
                    ext = ".html";
                    bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
                        "<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n<title>Clip</title>\n</head>\n<body>\n"
                        + fragment + "\n</body>\n</html>");
                }
                else
                {
                    var planned = ClipExport.Prepare(item, formats, "auto");
                    if (!planned.Ok) throw new InvalidOperationException(planned.Error);
                    ext = planned.Extension;
                    bytes = planned.Bytes;
                }
            }

            string dir = Path.Combine(Path.GetTempPath(), "Clip");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, ClipExport.SuggestedName(item.Text, ext, CapturedAt(item.CapturedAt)));
            try
            {
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new InvalidOperationException("Could not open the clip: " + ex.Message);
            }
            return (path, ext);
        }

        /// <summary>The clip's captured time via the existing read-only helper; the fallback name is stamped with it.</summary>
        static string CapturedAtOf(string dbPath, long id)        {
            using var conn = OpenRead(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT captured_at FROM items WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteScalar() as string;
        }

        static DateTime CapturedAt(string stored) =>
            DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)
                ? when
                : DateTime.Now;
    }

    /// <summary>
    /// A host dialog holds the shell. The popup hides on any Deactivated, which is what opening a dialog
    /// produces, so the shell asks this gate first: while a dialog is open, losing activation is the dialog,
    /// not the user clicking away. A count, so a dialog opened from a dialog cannot re-enable the hide early.
    /// </summary>
    internal sealed class ModalGate
    {
        int _open;

        public bool Open => _open > 0;

        public void Enter() => _open++;

        public void Leave()
        {
            if (_open > 0) _open--;
        }
    }
}
