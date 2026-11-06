using System;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SSMSTabManager
{
    internal sealed class DocumentOpenColorizer : IVsRunningDocTableEvents
    {
        private readonly DTE dte;
        private readonly IVsUIShell shell;

        public DocumentOpenColorizer(DTE dte, IVsUIShell shell)
        {
            this.dte = dte;
            this.shell = shell;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (fFirstShow == 0)
            {
                return VSConstants.S_OK;
            }

            try
            {
                pFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var monikerObject);
                var moniker = monikerObject as string;
                var document = FindDocument(moniker);
                if (document == null)
                {
                    return VSConstants.S_OK;
                }

                var lines = SSMSTabManagerCommand.ReadFirstLines(document);
                var color = TabColor.FromHeader(lines[0], lines[1]);
                if (color != null)
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        TabColorizer.Apply(shell, moniker, color);
                    }));
                }
            }
            catch (Exception)
            {
            }
            return VSConstants.S_OK;
        }

        private Document FindDocument(string moniker)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.IsNullOrEmpty(moniker))
            {
                return null;
            }

            foreach (Document document in dte.Documents)
            {
                if (string.Equals(document.FullName, moniker, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
            return null;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
    }
}
