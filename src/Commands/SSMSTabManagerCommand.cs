using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace SSMSTabManager
{
    internal sealed class SSMSTabManagerCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("0fd30064-c41d-4780-9a22-670f31bc4bcf");

        private const int FirstColorCommandId = 0x0201;
        private static readonly string[] ColorCommandNames = { "červená", "zelená", "modrá", "žlutá", "fialová", null };

        private readonly AsyncPackage package;
        private readonly DTE dte;
        private readonly IVsUIShell shell;
        private readonly IVsRunningDocumentTable documentTable;
        private readonly IVsTextManager textManager;

        private SSMSTabManagerCommand(AsyncPackage package, OleMenuCommandService commandService, DTE dte, IVsUIShell shell,
            IVsRunningDocumentTable documentTable, IVsTextManager textManager)
        {
            this.package = package;
            this.dte = dte;
            this.shell = shell;
            this.documentTable = documentTable;
            this.textManager = textManager;
            commandService.AddCommand(new MenuCommand(Execute, new CommandID(CommandSet, CommandId)));
            for (var i = 0; i < ColorCommandNames.Length; i++)
            {
                var colorName = ColorCommandNames[i];
                commandService.AddCommand(new MenuCommand((s, e) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    ExecuteColor(colorName);
                }, new CommandID(CommandSet, FirstColorCommandId + i)));
            }
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = (OleMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            var dte = (DTE)await package.GetServiceAsync(typeof(DTE));
            var shell = (IVsUIShell)await package.GetServiceAsync(typeof(SVsUIShell));
            var documentTable = (IVsRunningDocumentTable)await package.GetServiceAsync(typeof(SVsRunningDocumentTable));
            var textManager = (IVsTextManager)await package.GetServiceAsync(typeof(SVsTextManager));
            new SSMSTabManagerCommand(package, commandService, dte, shell, documentTable, textManager);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var status = SaveActiveDocument();
                var colorStatus = ColorActiveTab();
                var bookmarkStatus = BookmarkActiveDocument();
                var statuses = new[] { status, colorStatus, bookmarkStatus }.Where(s => s != null).ToArray();
                if (statuses.Length > 0)
                {
                    SetStatus(string.Join(" | ", statuses));
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Chyba při ukládání:\n" + ex.Message);
            }
        }

        private void ExecuteColor(string colorName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var document = dte.ActiveDocument;
                if (document == null)
                {
                    ShowMessage("Žádný aktivní dokument.");
                    return;
                }

                UpdateColorLine(document, colorName);
                if (!TabColorizer.Apply(shell, document.FullName, TabColor.FromName(colorName)))
                {
                    SetStatus("SSMSTabManager: záložku pro obarvení se nepodařilo najít");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Chyba při obarvení:\n" + ex.Message);
            }
        }

        private static void UpdateColorLine(Document document, string colorName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var lines = ReadFirstLines(document);
            if (string.IsNullOrEmpty(QueryFileName.GetCommentTitle(lines[0])))
            {
                return;
            }

            var textDocument = (TextDocument)document.Object("TextDocument");
            var hasColorLine = textDocument.EndPoint.Line >= 2 && TabColor.IsColorComment(lines[1]);
            var secondLineStart = textDocument.StartPoint.CreateEditPoint();
            secondLineStart.LineDown(1);
            secondLineStart.StartOfLine();

            if (colorName == null)
            {
                if (hasColorLine)
                {
                    var thirdLineStart = secondLineStart.CreateEditPoint();
                    thirdLineStart.LineDown(1);
                    thirdLineStart.StartOfLine();
                    if (thirdLineStart.Line == secondLineStart.Line)
                    {
                        var firstLineEnd = textDocument.StartPoint.CreateEditPoint();
                        firstLineEnd.EndOfLine();
                        thirdLineStart.EndOfLine();
                        firstLineEnd.Delete(thirdLineStart);
                    }
                    else
                    {
                        secondLineStart.Delete(thirdLineStart);
                    }
                }
                return;
            }

            var colorLine = "-- " + colorName;
            if (hasColorLine)
            {
                var secondLineEnd = secondLineStart.CreateEditPoint();
                secondLineEnd.EndOfLine();
                secondLineStart.ReplaceText(secondLineEnd, colorLine, 0);
            }
            else
            {
                var firstLineEnd = textDocument.StartPoint.CreateEditPoint();
                firstLineEnd.EndOfLine();
                firstLineEnd.Insert(Environment.NewLine + colorLine);
            }
        }

        private string ColorActiveTab()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var document = dte.ActiveDocument;
            if (document == null)
            {
                return null;
            }

            var lines = ReadFirstLines(document);
            var color = TabColor.FromHeader(lines[0], lines[1]);
            if (color == null)
            {
                return null;
            }

            if (!TabColorizer.Apply(shell, document.FullName, color))
            {
                return "SSMSTabManager: záložku pro obarvení se nepodařilo najít";
            }
            return null;
        }

        private string BookmarkActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var document = dte.ActiveDocument;
            if (document == null)
            {
                return null;
            }

            var title = QueryFileName.GetCommentTitle(ReadFirstLines(document)[0]);
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }
            return QueryBookmark.Apply(dte, documentTable, textManager, document, title, Log);
        }

        private void Log(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var outputWindow = (OutputWindow)dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
            var panes = outputWindow.OutputWindowPanes;
            OutputWindowPane pane = null;
            foreach (OutputWindowPane existing in panes)
            {
                if (existing.Name == "SSMSTabManager")
                {
                    pane = existing;
                }
            }
            (pane ?? panes.Add("SSMSTabManager")).OutputString(DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine);
        }

        internal static string[] ReadFirstLines(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var textDocument = (TextDocument)document.Object("TextDocument");
            var text = textDocument.StartPoint.CreateEditPoint().GetLines(1, Math.Min(3, textDocument.EndPoint.Line + 1));
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return new[] { lines[0], lines.Length > 1 ? lines[1] : string.Empty };
        }

        private string SaveActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var document = dte.ActiveDocument;
            if (document == null)
            {
                ShowMessage("Žádný aktivní dokument.");
                return null;
            }

            var firstLine = ReadFirstLines(document)[0];

            if (IsSavedFile(document.FullName))
            {
                return RenameSavedDocument(document, firstLine);
            }

            var targetDirectory = ExtensionSettings.Load().TargetDirectory;
            var now = DateTime.Now;
            Directory.CreateDirectory(targetDirectory);
            var name = QueryFileName.FromFirstLine(firstLine, now);
            var path = QueryFileName.GetUniquePath(targetDirectory, name, now);

            document.Save(path);
            return "SSMSTabManager: uloženo do " + path;
        }

        private string RenameSavedDocument(Document document, string firstLine)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var targetDirectory = ExtensionSettings.Load().TargetDirectory;
            var oldPath = document.FullName;
            if (!IsInDirectory(oldPath, targetDirectory))
            {
                ShowMessage("Dokument je uložený mimo " + targetDirectory + ", nic nedělám:\n" + oldPath);
                return null;
            }

            var title = QueryFileName.GetCommentTitle(firstLine);
            if (string.IsNullOrEmpty(title))
            {
                ShowMessage("Dokument už je uložený a první řádek nezačíná --, nic nedělám:\n" + oldPath);
                return null;
            }

            if (string.Equals(Path.GetFileNameWithoutExtension(oldPath), title, StringComparison.OrdinalIgnoreCase))
            {
                return "SSMSTabManager: název už odpovídá prvnímu řádku";
            }

            var newPath = QueryFileName.GetUniquePath(targetDirectory, title, DateTime.Now);
            var question = "Dokument je už uložený. Přejmenovat ho podle prvního řádku?\n\n"
                + Path.GetFileName(oldPath) + "\n→ " + Path.GetFileName(newPath);
            if (!AskYesNo(question))
            {
                return null;
            }

            document.Save(newPath);
            File.Delete(oldPath);
            return "SSMSTabManager: přejmenováno na " + newPath;
        }

        private static bool IsInDirectory(string fullName, string targetDirectory)
        {
            var directory = Path.GetFullPath(Path.GetDirectoryName(fullName)).TrimEnd('\\');
            return string.Equals(directory, targetDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSavedFile(string fullName)
        {
            if (!File.Exists(fullName))
            {
                return false;
            }

            var directory = Path.GetFullPath(Path.GetDirectoryName(fullName)).TrimEnd('\\');
            var tempDirectory = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
            return !string.Equals(directory, tempDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private void SetStatus(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.StatusBar.Text = text;
        }

        private bool AskYesNo(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = VsShellUtilities.ShowMessageBox(package, text, "SSMSTabManager",
                OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            return result == (int)VSConstants.MessageBoxResult.IDYES;
        }

        private void ShowMessage(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(package, text, "SSMSTabManager",
                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
