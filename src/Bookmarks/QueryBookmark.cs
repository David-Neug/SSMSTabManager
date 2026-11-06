using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace SSMSTabManager
{
    internal static class QueryBookmark
    {
        public static string Apply(DTE dte, IVsRunningDocumentTable documentTable, IVsTextManager textManager, Document document, string title, Action<string> log)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            log("bookmark: dokument " + document.FullName + ", název '" + title + "'");
            var lines = GetTextLines(documentTable, document.FullName);
            if (lines == null)
            {
                return "SSMSTabManager: bookmark – buffer dokumentu nenalezen";
            }

            ErrorHandler.ThrowOnFailure(((IVsPrivTextManager)textManager).GetBookmarkManager(out var manager));
            var markers = GetFirstLineBookmarks(lines);
            log("bookmark: na 1. řádku nalezeno " + markers.Count + " bookmarků");
            foreach (var marker in markers)
            {
                log("bookmark: RemoveItem hr=0x" + manager.RemoveItem(marker).ToString("X8"));
            }

            var editPoint = ((TextDocument)document.Object("TextDocument")).StartPoint.CreateEditPoint();
            editPoint.ClearBookmark();
            log("bookmark: po odstranění zbývá " + GetFirstLineBookmarks(lines).Count + " bookmarků");
            ToggleBookmarkOnFirstLine(dte, document);
            log("bookmark: po Edit.ToggleBookmark je na 1. řádku " + GetFirstLineBookmarks(lines).Count + " bookmarků");

            var hr = manager.GetCurrentBookmark(out var item);
            log("bookmark: GetCurrentBookmark hr=0x" + hr.ToString("X8") + ", item=" + (item != null));
            if (hr != VSConstants.S_OK || item == null)
            {
                return "SSMSTabManager: bookmark vložen, ale bez názvu (aktuální bookmark nenalezen)";
            }

            item.Document(out var itemDocument);
            item.Line(out var itemLine);
            item.get_Text(out var oldName);
            log("bookmark: aktuální bookmark '" + oldName + "' v " + itemDocument + ", řádek " + (itemLine + 1));
            var isOtherDocument = !string.IsNullOrEmpty(itemDocument)
                && !string.Equals(itemDocument, document.FullName, StringComparison.OrdinalIgnoreCase);
            if (itemLine != 0 || isOtherDocument)
            {
                return "SSMSTabManager: bookmark vložen, ale bez názvu (aktuální bookmark je " + itemDocument + ":" + (itemLine + 1) + ")";
            }

            log("bookmark: put_Text hr=0x" + item.put_Text(title).ToString("X8"));
            item.get_Text(out var newName);
            log("bookmark: název po put_Text '" + newName + "'");

            item.GetItemHandle(out var treeItem);
            log("bookmark: GetItemHandle 0x" + treeItem.ToString("X"));
            if (treeItem == IntPtr.Zero || !TreeViewRefresher.SetItemText(treeItem, title, log))
            {
                return "SSMSTabManager: bookmark pojmenován, ale okno Bookmarks se nepodařilo aktualizovat";
            }
            return null;
        }

        private static void ToggleBookmarkOnFirstLine(DTE dte, Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var selection = (TextSelection)document.Selection;
            var anchorLine = selection.AnchorPoint.Line;
            var anchorColumn = selection.AnchorPoint.LineCharOffset;
            var activeLine = selection.ActivePoint.Line;
            var activeColumn = selection.ActivePoint.LineCharOffset;

            selection.MoveToLineAndOffset(1, 1);
            try
            {
                dte.ExecuteCommand("Edit.ToggleBookmark");
            }
            finally
            {
                selection.MoveToLineAndOffset(anchorLine, anchorColumn);
                selection.MoveToLineAndOffset(activeLine, activeColumn, true);
            }
        }

        private static IVsTextLines GetTextLines(IVsRunningDocumentTable documentTable, string moniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var docData = IntPtr.Zero;
            try
            {
                var hr = documentTable.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, moniker,
                    out _, out _, out docData, out _);
                if (hr != VSConstants.S_OK || docData == IntPtr.Zero)
                {
                    return null;
                }

                var data = Marshal.GetObjectForIUnknown(docData);
                if (data is IVsTextLines lines)
                {
                    return lines;
                }
                if (data is IVsTextBufferProvider provider && provider.GetTextBuffer(out var buffer) == VSConstants.S_OK)
                {
                    return buffer;
                }
                return null;
            }
            finally
            {
                if (docData != IntPtr.Zero)
                {
                    Marshal.Release(docData);
                }
            }
        }

        private static List<IVsTextLineMarker> GetFirstLineBookmarks(IVsTextLines lines)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var markers = new List<IVsTextLineMarker>();
            const uint flags = (uint)(ENUMMARKERFLAGS.EM_ENTIREBUFFER | ENUMMARKERFLAGS.EM_INCLUDEINVISIBLE);
            var hr = lines.EnumMarkers(0, 0, 0, 0, (int)MARKERTYPE.MARKER_BOOKMARK, flags, out var markerEnum);
            if (hr != VSConstants.S_OK || markerEnum == null)
            {
                return markers;
            }

            var span = new TextSpan[1];
            while (markerEnum.Next(out var marker) == VSConstants.S_OK && marker != null)
            {
                if (marker.GetCurrentSpan(span) == VSConstants.S_OK && span[0].iStartLine == 0)
                {
                    markers.Add(marker);
                }
            }
            return markers;
        }

        [ComImport]
        [Guid("F584B25E-AC79-48AC-933F-CF285F8882A4")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVsPrivTextManager
        {
            [PreserveSig]
            int GetBookmarkManager([MarshalAs(UnmanagedType.Interface)] out IVsBookmarkManager manager);
        }

        [ComImport]
        [Guid("D9F127B8-5D54-418c-9399-E3A2D850D983")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVsBookmarkManager
        {
            [PreserveSig] int CreateItem(int shortcutLine, IntPtr buffer, IntPtr moniker, int bookmarkId, out IntPtr item);
            [PreserveSig] int RemoveItem([MarshalAs(UnmanagedType.Interface)] IVsTextLineMarker marker);
            [PreserveSig] int ResetCurBookmark();
            [PreserveSig] int WriteUserOptions(IntPtr stream);
            [PreserveSig] int ReadUserOptions(IntPtr stream);
            [PreserveSig] int MoveItem(IntPtr item, IntPtr itemAfter, int intoFolder);
            [PreserveSig] int GoToBookmark(int folder, int forward);
            [PreserveSig] int DeleteAll();
            [PreserveSig] int DeleteAllInBuffer(IntPtr buffer);
            [PreserveSig] int SetCurrentBookmark(IntPtr item);
            [PreserveSig] int GetCurrentBookmark([MarshalAs(UnmanagedType.Interface)] out IVsBookmarkItem item);
        }

        [ComImport]
        [Guid("5B854029-17C5-4fb9-897A-16B40055EF51")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVsBookmarkItem
        {
            [PreserveSig] int SetItemHandle(IntPtr treeItem);
            [PreserveSig] int GetItemHandle(out IntPtr treeItem);
            [PreserveSig] int NavigateTo();
            [PreserveSig] int GetType(out int type);
            [PreserveSig] int GetMarker(IntPtr marker);
            [PreserveSig] int GetID(out int id);
            [PreserveSig] int GetObjList(IntPtr item);
            [PreserveSig] int GetNext(IntPtr item);
            [PreserveSig] int get_Checked(out int isChecked);
            [PreserveSig] int get_Text([MarshalAs(UnmanagedType.BStr)] out string name);
            [PreserveSig] int put_Text([MarshalAs(UnmanagedType.BStr)] string name);
            [PreserveSig] int Scope([MarshalAs(UnmanagedType.BStr)] out string scope);
            [PreserveSig] int Document([MarshalAs(UnmanagedType.BStr)] out string moniker);
            [PreserveSig] int Line(out int line);
        }
    }
}
