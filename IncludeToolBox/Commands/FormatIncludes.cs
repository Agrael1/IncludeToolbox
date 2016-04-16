﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCProjectEngine;

namespace IncludeToolbox.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class FormatIncludes
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormatIncludes"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private FormatIncludes(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(MenuCommandSet.Guid, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static FormatIncludes Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new FormatIncludes(package);
        }

        /// <summary>
        /// Returns process selection range - whole lines!
        /// </summary>
        SnapshotSpan GetSelectionSpan(IWpfTextViewHost viewHost)
        {
            var sel = viewHost.TextView.Selection.StreamSelectionSpan;
            var start = new SnapshotPoint(viewHost.TextView.TextSnapshot, sel.Start.Position).GetContainingLine().Start;
            var end = new SnapshotPoint(viewHost.TextView.TextSnapshot, sel.End.Position).GetContainingLine().End;

            return new SnapshotSpan(start, end);
        }

        private void FormatPaths(FormatterOptionsPage.PathMode pathformat, bool ignoreFileRelative, IncludeLineInfo[] lines, List<string> includeDirectories)
        {
            if (pathformat == FormatterOptionsPage.PathMode.Unchanged)
                return;

            foreach (var line in lines)
            {
                // todo: Ignore std library files.
                if (line.AbsoluteIncludePath != null)
                {
                    int bestLength = Int32.MaxValue;
                    string bestCandidate = null;

                    int i = ignoreFileRelative ? 1 : 0; // Ignore first one which is always the local dir.
                    for(; i< includeDirectories.Count; ++i)
                    {
                        string proposal = Utils.MakeRelative(includeDirectories[i], line.AbsoluteIncludePath);

                        if (proposal.Length < bestLength)
                        {
                            if (pathformat == FormatterOptionsPage.PathMode.Shortest || (proposal.IndexOf("../") < 0 && proposal.IndexOf("..\\") < 0))
                            {
                                bestCandidate = proposal;
                                bestLength = proposal.Length;
                            }
                        }
                    }

                    if (bestCandidate != null)
                    {
                        line.IncludeContent = bestCandidate;
                    }
                }
            }
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            var settings = (FormatterOptionsPage)package.GetDialogPage(typeof(FormatterOptionsPage));

            // Try to find absolute paths
            var document = Utils.GetActiveDocument();
            var project = document.ProjectItem.ContainingProject;
            if (project == null)
            {
                Output.Instance.WriteLine("The document {0} is not part of a project.", document.Name);
                return;
            }
            var includeDirectories = Utils.GetProjectIncludeDirectories(project);
            includeDirectories.Insert(0, PathUtil.Normalize(document.Path) + Path.DirectorySeparatorChar);
            
            // Read.
            var viewHost = Utils.GetCurrentViewHost();
            var selectionSpan = GetSelectionSpan(viewHost);
            var lines = IncludeLineInfo.ParseIncludes(selectionSpan.GetText(), settings.RemoveEmptyLines, includeDirectories);

            // Manipulate paths.
            FormatPaths(settings.PathFormat, settings.IgnoreFileRelative, lines, includeDirectories);

            // Format.
            switch (settings.DelimiterFormatting)
            {
                case FormatterOptionsPage.DelimiterMode.AngleBrackets:
                    foreach (var line in lines)   
                        line.SetLineType(IncludeLineInfo.Type.AngleBrackets);
                    break;
                case FormatterOptionsPage.DelimiterMode.Quotes:
                    foreach (var line in lines)
                        line.SetLineType(IncludeLineInfo.Type.Quotes);
                    break;
            }
            switch (settings.SlashFormatting)
            {
                case FormatterOptionsPage.SlashMode.ForwardSlash:
                    foreach (var line in lines)
                        line.IncludeContent = line.IncludeContent.Replace('\\', '/');
                    break;
                case FormatterOptionsPage.SlashMode.BackSlash:
                    foreach (var line in lines)
                        line.IncludeContent = line.IncludeContent.Replace('/', '\\');
                    break;
            }

            // Apply changes so far.
            foreach (var line in lines)
                line.UpdateTextWithIncludeContent();

            // Sorting. Ignores non-include lines.
            var comparer = new IncludeComparer(settings.PrecedenceRegexes, document);
            var sortedIncludes = lines.Where(x => x.LineType != IncludeLineInfo.Type.NoInclude).OrderBy(x => x.IncludeContent, comparer).ToArray();
            int incIdx = 0;
            for (int allIdx = 0; allIdx < lines.Length && incIdx < sortedIncludes.Length; ++allIdx)
            {
                if (lines[allIdx].LineType != IncludeLineInfo.Type.NoInclude)
                {
                    lines[allIdx] = sortedIncludes[incIdx];
                    ++incIdx;
                }
            }


            // Overwrite.
            string replaceText = string.Join(Environment.NewLine, lines.Select(x => x.Text));
            using (var edit = viewHost.TextView.TextBuffer.CreateEdit())
            {
                edit.Replace(selectionSpan, replaceText);
                edit.Apply();
            }
        }
    }
}
