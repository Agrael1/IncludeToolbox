﻿using Community.VisualStudio.Toolkit;
using IncludeToolbox.Commands;
using IncludeToolbox.IncludeWhatYouUse;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace IncludeToolbox
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class IWYUOptionsPage : BaseOptionPage<IWYUOptions> { }
    }

    public class IWYUOptions : BaseOptionModel<IWYUOptions>
    {

        string exe = "";
        Comment comm = Comment.Yes;
        Substitution subs = Substitution.Cheap;
        uint verbose = 2;
        bool? download_required = null;
        bool pch = false;
        bool nodefault = false;
        bool transitives = false;
        bool warn = false;
        bool always = false;
        bool header = false;
        bool format = false;
        bool dirty = true;
        string mapping = "";
        string[] clang_options = new string[] { };
        string[] iwyu_options = new string[] { };

        public bool GetDirty()
        {
            return dirty;
        }

        private void SetDirty(bool value)
        {
            dirty = value;
        }

        public async Task<bool> DownloadRequiredAsync()
        {
            if (download_required != null) return download_required.Value;
            if (Executable == IWYUDownload.GetDefaultExecutablePath())
                download_required = await IWYUDownload.IsNewerVersionAvailableOnlineAsync();
            return download_required.Value;
        }
        public void Downloaded()
        {
            download_required = false;
        }

        [Category("General")]
        [DisplayName("IWYU Executable")]
        [Description("A path to IWYU executable.")]
        [DefaultValue("")]
        public string Executable
        {
            get { return exe; }
            set
            {
                SetDirty(true);
                exe = value;
            }
        }

        [Category("General")]
        [DisplayName("Print Commentaries")]
        [Description("Tells IWYU to show or hide individual commentaries to headers.")]
        [DefaultValue(Comment.Yes)]
        [TypeConverter(typeof(EnumConverter))]
        public Comment Comms { get { return comm; } set { SetDirty(true); comm = value; } }
        
        [Category("General")]
        [DisplayName("Output Verbosity")]
        [Description("Determines how much output needs to be printed. May help in case of error. Max value is 7.")]
        [DefaultValue(2)]
        public uint Verbosity{ get { return verbose; } set { SetDirty(true); verbose = Math.Min(value, 7); } }

        [Category("General")]
        [DisplayName("Precompiled Header")]
        [Description("Sets if first file in .cpp is precompiled header. Blocks first file from being parsed.")]
        [DefaultValue(false)]
        public bool Precompiled { get { return pch; } set { SetDirty(true); pch = value; } }
        
        [Category("General")]
        [DisplayName("No Default Maps")]
        [Description("If true, turns default gcc iwyu STL bindings off. Useful for STL map implementation.")]
        [DefaultValue(false)]
        public bool NoDefault { get { return nodefault; } set { SetDirty(true); nodefault = value; } }
        
        [Category("General")]
        [DisplayName("Only Transitive")]
        [Description("Do not suggest that a file add foo.h unless foo.h is already visible in the file's transitive includes.")]
        [DefaultValue(false)]
        public bool Transitives { get { return transitives; } set { SetDirty(true); transitives = value; } }        
        
        [Category("General")]
        [DisplayName("Show Warnings")]
        [Description("Shows warnings from IWYU compiler.")]
        [DefaultValue(false)]
        public bool Warnings { get { return warn; } set { SetDirty(true); warn = value; } }        
        
        [Category("General")]
        [DisplayName("Always Rebuild")]
        [Description("Rebuild the project command line on each call. Good for dynamic projects, that may change their options.")]
        [DefaultValue(false)]
        public bool AlwaysRebuid { get { return always; } set { SetDirty(true); always = value; } }

        [Category("General")]
        [DisplayName("Substitution Mode")]
        [Description("Choose the model of substitution for headers. If includes are scattered across the file, the mode is precise. Cheap is used when includes are a block before any code. If used wrong, Cheap may remove code between first and the last include.")]
        [TypeConverter(typeof(EnumConverter))]
        public Substitution Sub { get { return subs; } set { subs = value; SetDirty(true); } }


        [Category("Options")]
        [DisplayName("IWYU options")]
        [Description("IWYU launch options, that determine the flow of include-what-you-use.")]
        public string[] Options { get { return iwyu_options; } set { iwyu_options = value; SetDirty(true); } }

        [Category("Options")]
        [DisplayName("Clang options")]
        [Description("Clang launch options, that determine compilation stage flow.")]
        public string[] ClangOptions { get { return clang_options; } set { clang_options = value; SetDirty(true); } }
        
        [Category("Options")]
        [DisplayName("Mapping File")]
        [Description("Specifies the mapping file to use by iwyu.")]
        [DefaultValue("")]
        public string MappingFile { get { return mapping; } set { mapping = value; SetDirty(true); } }

        [Category("Options")]
        [DisplayName("Ignore Header")]
        [Description("Ignores header of specified .cpp. Tries to find same-named .h in the includes. If it succeeds, the header is moved to the beginning and it is treated as precompiled. Useful when .h file is already refactored.")]
        [DefaultValue(false)]
        public bool IgnoreHeader { get { return header; } set { header = value; SetDirty(true); } }
        
        [Category("Options")]
        [DisplayName("Format After")]
        [Description("Uses formatting tool after results of IWYU are applied.")]
        [DefaultValue(false)]
        public bool Format { get => format; set { format = value; SetDirty(true); } }

        public void ClearFlag()
        {
            SetDirty(false);
        }
    }

    public enum Comment
    {
        Yes,
        No
    }
    public enum Substitution
    {
        Cheap,
        Precise
    }
}
