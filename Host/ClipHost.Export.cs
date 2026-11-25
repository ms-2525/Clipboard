using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Clip.Core;
using Microsoft.Win32;

namespace Clip.Host
{
    /// <summary>
    /// The "Save as" path. Conversion, the offered name and every refusal live in <see cref="ClipExport"/>;
    /// this file owns only the OS dialog and the one write to the path the user confirmed.
    /// </summary>
    internal sealed partial class ClipHost
    {
        /// <summary>
        /// Bridge method "export". Runs on the UI thread (the dispatcher's nested message loop keeps the page
        /// alive while the dialog is open), so no lock and no async around ShowDialog.
        /// </summary>
        object Export(JsonElement p) =>
            Export(_store, _dbPath, Long(p, "id"), String(p, "format") ?? "auto", AskPath, _modals, _show);

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

        /// <summary>The clip's captured time via the existing read-only helper; the fallback name is stamped with it.</summary>
        static string CapturedAtOf(string dbPath, long id)
        {
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
