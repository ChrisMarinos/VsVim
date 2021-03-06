﻿#light

namespace DefaultVimHost
open Vim

type VimHost() =
    interface IVimHost with 
        member x.UpdateStatus _ = ()
        member x.UpdateLongStatus _ = ()
        member x.Beep () = System.Console.Beep()
        member x.OpenFile _ = ()
        member x.Undo _ _ = ()
        member x.Redo _ _ = ()
        member x.GoToDefinition() = false
        member x.GoToMatch() = false
        member x.NavigateTo _ = false
        member x.GetName _ = ""
        member x.ShowOpenFileDialog () = ()
