using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Clip.Core;

namespace Clip.Host
{
    /// <summary>
    /// Tab CRUD for the page. Every method answers on the UI thread: these touch a handful of rows, so there is
    /// no reader-thread hop. Names and duplicates are validated by <see cref="ClipboardStore"/> with the same
    /// messages the old inline Ctrl+T editor showed; this file only adds the Pinned-tab refusals and the reply
    /// shapes.
    /// </summary>
    internal sealed partial class ClipHost
    {
        /// <summary>Bridge method "listTabs": the same strip init ships, plus the built-in Pinned id.</summary>
        object ListTabs(JsonElement p) => ListTabs(_store, _pinnedTabId);

        /// <summary>Order is the store's <c>sort_order, id</c>: Pinned first, then custom tabs by creation.</summary>
        internal static object ListTabs(ClipboardStore store, long pinnedTabId) => new
        {
            tabs = store.ListTabs()
                .Select(t => new { id = t.id, name = t.name, kind = t.kind, count = t.count })
                .ToList(),
            pinnedTabId
        };

        /// <summary>Bridge method "createTab". Blank, too long, control chars and duplicates throw the old messages.</summary>
        object CreateTab(JsonElement p) => CreateTab(_store, String(p, "name"));

        internal static object CreateTab(ClipboardStore store, string name)
        {
            long id = store.CreateTab(name);
            var tab = store.ListTabs().First(t => t.id == id);
            return new { id = tab.id, name = tab.name, kind = tab.kind, count = tab.count };
        }

        /// <summary>Bridge method "renameTab". Pinned is refused; the store applies the create rules to the name.</summary>
        object RenameTab(JsonElement p) =>
            RenameTab(_store, _pinnedTabId, Long(p, "id"), String(p, "name"));

        internal static object RenameTab(ClipboardStore store, long pinnedTabId, long id, string name)
        {
            if (id == pinnedTabId) throw new ArgumentException("The Pinned tab cannot be renamed.");
            if (!store.ListTabs().Any(t => t.id == id)) throw new ArgumentException("no tab with id " + id);
            // false means the store found the new name taken (the blank/size/control checks throw their own message).
            if (!store.RenameTab(id, name)) throw new ArgumentException("A tab with that name already exists.");
            return new { renamed = true };
        }

        /// <summary>
        /// Bridge method "deleteTab". <see cref="ClipboardStore.DeleteTab"/> PURGES the tab's clips and format
        /// blobs; it does not move them back to history. The reply names both truths so the page never tells the
        /// user clips were rescued when they were deleted.
        /// </summary>
        object DeleteTab(JsonElement p) => DeleteTab(_store, _pinnedTabId, Long(p, "id"));

        internal static object DeleteTab(ClipboardStore store, long pinnedTabId, long id)
        {
            if (id == pinnedTabId) throw new ArgumentException("The Pinned tab cannot be deleted.");
            if (!store.ListTabs().Any(t => t.id == id)) throw new ArgumentException("no tab with id " + id);

            int purged = store.DeleteTab(id); // -1 would mean a non-custom row slipped past the two checks above
            if (purged < 0) throw new ArgumentException("no deletable tab with id " + id);
            return new { deleted = true, deletedClips = purged, movedToHistory = 0 };
        }
        /// <summary>
        /// Bridge method "reorderTabs": persists the custom-tab strip order after a page drag. Pinned stays
        /// first no matter where the page puts it; unknown ids are ignored by <see cref="ClipboardStore"/>.
        /// </summary>
        object ReorderTabs(JsonElement p) => ReorderTabs(_store, p);

        internal static object ReorderTabs(ClipboardStore store, JsonElement p)
        {
            if (!p.TryGetProperty("order", out var arr) || arr.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("order required");
            var ids = new List<long>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number) ids.Add(el.GetInt64());
                else if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out long n)) ids.Add(n);
                else throw new ArgumentException("order must be tab ids");
            }
            store.SetTabOrder(ids.ToArray());
            return new { reordered = true };
        }
    }
}
