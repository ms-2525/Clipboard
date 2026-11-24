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
        object Export(JsonElement p)
        {
            long id = Long(p, "id");
            string requested = String(p, "format") ?? "auto";

            var stored = _store.GetImage(id);
            if (stored == null) throw new ArgumentException("no clip with id " + id);
            var formats = _store.GetFormats(id).Select(f => f.fmt).ToList();
            var item = new ClipboardItem
            {
                Id = id,
                Kind = stored.Kind,
                Text = _store.GetText(id),
                Image = stored.Image,
                CapturedAt = CapturedAtOf(id)
            };

            // Refuses a format that contradicts the kind (png for text, txt for a spreadsheet) before any dialog.
            var planned = ClipExport.Prepare(item, formats, requested);
            if (!planned.Ok) throw new InvalidOperationException(planned.Error);

            var dlg = new SaveFileDialog
            {
                Title = "Save clip",
                FileName = ClipExport.SuggestedName(item.Text, planned.Extension, CapturedAt(item.CapturedAt)),
                DefaultExt = planned.Extension,
                AddExtension = true,
                OverwritePrompt = true,
                ValidateNames = true,
                CheckPathExists = true,
                Filter = Filter(planned.Kind)
            };
            // The owner keeps the dialog in front of the app instead of behind it.
            if (dlg.ShowDialog(Window.GetWindow(_web)) != true)
                return new { saved = false, cancelled = true };

            // The user can type another extension in the box; it still has to match the clip's kind.
            var final = ClipExport.Prepare(item, formats, Path.GetExtension(dlg.FileName));
            if (!final.Ok) throw new InvalidOperationException(final.Error);

            try
            {
                ClipExport.Write(dlg.FileName, final.Bytes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // PathTooLongException is an IOException; the page toasts this sentence.
                throw new InvalidOperationException("Could not save the clip: " + ex.Message);
            }
            return new { saved = true, path = dlg.FileName };
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
        string CapturedAtOf(long id)
        {
            using var conn = OpenRead(_dbPath);
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
}
