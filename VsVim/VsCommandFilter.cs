﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Vim;
using Microsoft.VisualStudio.OLE.Interop;
using IServiceProvider = System.IServiceProvider;
using Microsoft.VisualStudio.Shell;

namespace VsVim
{
    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandFilter : IOleCommandTarget
    {
        private readonly IVimBuffer _buffer;
        private readonly IVsTextView _textView;
        private readonly IOleCommandTarget _nextTarget;
        private readonly IServiceProvider _serviceProvider;

        internal VsCommandFilter(IVimBuffer buffer, IVsTextView view, IServiceProvider provider)
        {
            _buffer = buffer;
            _textView = view;
            _serviceProvider = provider;
            var hr = view.AddCommandFilter(this, out _nextTarget);
        }

        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput kiOutput)
        {
            kiOutput = null;

            // Don't ever process a command when we are in an automation function.  Doing so will cause VsVim to 
            // intercept items like running Macros and certain wizard functionality
            if (VsShellUtilities.IsInAutomationFunction(_serviceProvider))
            {
                return false;
            }

            EditCommand command;
            if (!OleCommandUtil.TryConvert(commandGroup, commandId, pvaIn, out command))
            {
                return false;
            }

            // If the current state of the buffer cannot process the command then do not convert it 
            if (!_buffer.CanProcess(command.KeyInput))
            {
                return false;
            }

            kiOutput = command.KeyInput;
            return true;
        }

        #region IOleCommandTarget implementation

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            KeyInput ki = null;
            if (OleCommandUtil.IsDebugIgnore(commandGroup, commandId)
                || !TryConvert(commandGroup, commandId, pvaIn, out ki)
                || !_buffer.Process(ki))
            {
                return _nextTarget.Exec(commandGroup, commandId, nCmdexecopt, pvaIn, pvaOut);
            }

            return NativeMethods.S_OK;
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            KeyInput ki = null;
            if (1 == cCmds
                && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out ki)
                && _buffer.CanProcess(ki))
            {
                prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                return NativeMethods.S_OK;
            }

            return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion


    }
}
