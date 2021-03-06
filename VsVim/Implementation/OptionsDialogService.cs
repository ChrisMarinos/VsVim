﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;

namespace VsVim.Implementation
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        public bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot)
        {
            return UI.ConflictingKeyBindingDialog.DoShow(snapshot);
        }
    }
}
