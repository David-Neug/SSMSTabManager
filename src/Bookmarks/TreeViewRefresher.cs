using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SSMSTabManager
{
    internal static class TreeViewRefresher
    {
        private const string TreeViewClassName = "SysTreeView32";
        private const int TVM_GETNEXTITEM = 0x1100 + 10;
        private const int TVM_SETITEMW = 0x1100 + 63;
        private const int TVGN_ROOT = 0x0;
        private const int TVGN_NEXT = 0x1;
        private const int TVGN_CHILD = 0x4;
        private const uint TVIF_TEXT = 0x1;
        private const uint TVIF_HANDLE = 0x10;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TVITEM
        {
            public uint mask;
            public IntPtr hItem;
            public uint state;
            public uint stateMask;
            public string pszText;
            public int cchTextMax;
            public int iImage;
            public int iSelectedImage;
            public int cChildren;
            public IntPtr lParam;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref TVITEM item);

        public static bool SetItemText(IntPtr item, string text, Action<string> log)
        {
            var trees = FindTreeViews();
            log("bookmark: nalezeno " + trees.Count + " stromů " + TreeViewClassName);
            foreach (var tree in trees)
            {
                if (!ContainsItem(tree, SendMessage(tree, TVM_GETNEXTITEM, (IntPtr)TVGN_ROOT, IntPtr.Zero), item))
                {
                    continue;
                }

                var tvItem = new TVITEM { mask = TVIF_TEXT | TVIF_HANDLE, hItem = item, pszText = text };
                var result = SendMessage(tree, TVM_SETITEMW, IntPtr.Zero, ref tvItem);
                log("bookmark: položka nalezena ve stromu 0x" + tree.ToString("X") + ", TVM_SETITEM=" + result);
                return result != IntPtr.Zero;
            }

            log("bookmark: položka 0x" + item.ToString("X") + " v žádném stromu nenalezena");
            return false;
        }

        private static bool ContainsItem(IntPtr tree, IntPtr current, IntPtr item)
        {
            for (; current != IntPtr.Zero; current = SendMessage(tree, TVM_GETNEXTITEM, (IntPtr)TVGN_NEXT, current))
            {
                if (current == item
                    || ContainsItem(tree, SendMessage(tree, TVM_GETNEXTITEM, (IntPtr)TVGN_CHILD, current), item))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<IntPtr> FindTreeViews()
        {
            var trees = new List<IntPtr>();
            var processId = (uint)Process.GetCurrentProcess().Id;
            EnumWindowsProc collect = (hwnd, _) =>
            {
                var className = new StringBuilder(64);
                GetClassName(hwnd, className, className.Capacity);
                if (className.ToString() == TreeViewClassName)
                {
                    trees.Add(hwnd);
                }
                return true;
            };
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId == processId)
                {
                    EnumChildWindows(hwnd, collect, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
            return trees;
        }
    }
}
