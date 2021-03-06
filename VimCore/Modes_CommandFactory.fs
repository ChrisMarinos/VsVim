﻿#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal CommandFactory( _operations : ICommonOperations, _capture : IMotionCapture ) = 

    member private x.CreateStandardMovementCommandsCore () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield (InputUtil.CharToKeyInput('h'), moveLeft)
            yield (InputUtil.VimKeyToKeyInput VimKey.LeftKey, moveLeft)
            yield (InputUtil.VimKeyToKeyInput VimKey.BackKey, moveLeft)
            yield (KeyInput('h', KeyModifiers.Control), moveLeft)
            yield (InputUtil.CharToKeyInput('l'), moveRight)
            yield (InputUtil.VimKeyToKeyInput VimKey.RightKey, moveRight)
            yield (InputUtil.CharToKeyInput ' ', moveRight)
            yield (InputUtil.CharToKeyInput('k'), moveUp)
            yield (InputUtil.VimKeyToKeyInput VimKey.UpKey, moveUp)
            yield (KeyInput('p', KeyModifiers.Control), moveUp)
            yield (InputUtil.CharToKeyInput('j'), moveDown)
            yield (InputUtil.VimKeyToKeyInput VimKey.DownKey, moveDown)
            yield (KeyInput('n', KeyModifiers.Control),moveDown)
            yield (KeyInput('j', KeyModifiers.Control),moveDown)        
        }

    member private x.CreateStandardMovementCommands() =
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (ki,func) ->
            let funcWithReg opt reg = 
                func (CommandUtil.CountOrDefault opt)
                Completed NoSwitch
            Command.SimpleCommand (OneKeyInput ki,CommandFlags.Movement, funcWithReg))

    /// Build up a set of MotionCommand values from applicable Motion values
    member private x.CreateMovementsFromMotions() =
        let processResult opt = 
            match opt with
            | None -> _operations.Beep()
            | Some(data) -> _operations.MoveCaretToMotionData data
            CommandResult.Completed NoSwitch

        let filterMotionCommand command = 
            match command with
            | SimpleMotionCommand(name,func) -> 
                let inner count _ =  func count |> processResult
                Command.SimpleCommand(name,CommandFlags.Movement,inner) |> Some
            | ComplexMotionCommand(_,false,_) -> None
            | ComplexMotionCommand(name,true,func) -> 
                
                let coreFunc count _ = 
                    let rec inner result =  
                        match result with
                        | ComplexMotionResult.Finished(func) ->
                            let res = 
                                match func count with
                                | None -> CommandResult.Error Resources.MotionCapture_InvalidMotion
                                | Some(data) -> 
                                    _operations.MoveCaretToMotionData data
                                    CommandResult.Completed NoSwitch 
                            res |> LongCommandResult.Finished
                        | ComplexMotionResult.NeedMoreInput (func) -> LongCommandResult.NeedMoreInput (fun ki -> func ki |> inner)
                        | ComplexMotionResult.Cancelled -> LongCommandResult.Cancelled
                        | ComplexMotionResult.Error (msg) -> CommandResult.Error msg |> LongCommandResult.Finished

                    let initialResult = func()
                    inner initialResult
                Command.LongCommand(name, CommandFlags.Movement, coreFunc) |> Some

        _capture.MotionCommands
        |> Seq.map filterMotionCommand
        |> SeqUtil.filterToSome

    /// Returns the set of commands which move the cursor.  This includes all motions which are 
    /// valid as movements.  Several of these are overridden with custom movement behavior though.
    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommands()
        let taken = standard |> Seq.map (fun command -> command.CommandName) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.CommandName))
        standard |> Seq.append motion

