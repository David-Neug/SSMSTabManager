using System;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SSMSTabManager
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class SSMSTabManagerPackage : AsyncPackage
    {
        public const string PackageGuidString = "699aba35-7f6b-497c-bc51-d75609816e4b";

        private IVsRunningDocumentTable documentTable;
        private uint documentEventsCookie;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await SSMSTabManagerCommand.InitializeAsync(this);

            var dte = (DTE)await GetServiceAsync(typeof(DTE));
            var shell = (IVsUIShell)await GetServiceAsync(typeof(SVsUIShell));
            documentTable = (IVsRunningDocumentTable)await GetServiceAsync(typeof(SVsRunningDocumentTable));
            if (dte != null && shell != null && documentTable != null)
            {
                documentTable.AdviseRunningDocTableEvents(new DocumentOpenColorizer(dte, shell), out documentEventsCookie);
            }
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing && documentTable != null && documentEventsCookie != 0)
            {
                documentTable.UnadviseRunningDocTableEvents(documentEventsCookie);
                documentEventsCookie = 0;
            }
            base.Dispose(disposing);
        }
    }
}
