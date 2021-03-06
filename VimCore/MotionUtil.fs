﻿#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal MotionUtil 
    ( 
        _textView : ITextView,
        _settings : IVimGlobalSettings) = 

    member x.StartPoint = TextViewUtil.GetCaretPoint _textView

    member private x.ForwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None 
        | Some(point:SnapshotPoint) -> 
            let span = SnapshotSpan(start, point.Add(1))
            {Span=span; IsForward=true; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Inclusive; Column=None} |> Some

    member private x.BackwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None
        | Some(point:SnapshotPoint) ->
            let span = SnapshotSpan(point, start)
            {Span=span; IsForward=false; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Exclusive; Column=None} |> Some

    member private x.LineToLineFirstNonWhitespaceMotion (originLine:ITextSnapshotLine) (endLine:ITextSnapshotLine) =
        let _,column = TssUtil.FindFirstNonWhitespaceCharacter endLine |> SnapshotPointUtil.GetLineColumn
        let span,isForward = 
            if originLine.LineNumber < endLine.LineNumber then
                let span = SnapshotSpan(originLine.Start, endLine.End)
                (span, true)
            elif originLine.LineNumber > endLine.LineNumber then
                let span = SnapshotSpan(endLine.Start, originLine.End)
                (span, false)
            else 
                let span = SnapshotSpan(endLine.Start, endLine.End)
                (span, true)
        {Span=span; IsForward=isForward; OperationKind=OperationKind.LineWise; MotionKind=MotionKind.Inclusive; Column= Some column }

    interface IMotionUtil with
        member x.TextView = _textView
        member x.ForwardChar c count = x.ForwardCharMotionCore c count TssUtil.FindNextOccurranceOfCharOnLine
        member x.ForwardTillChar c count = x.ForwardCharMotionCore c count TssUtil.FindTillNextOccurranceOfCharOnLine
        member x.BackwardChar c count = x.BackwardCharMotionCore c count TssUtil.FindPreviousOccurranceOfCharOnLine 
        member x.BackwardTillChar c count = x.BackwardCharMotionCore c count TssUtil.FindTillPreviousOccurranceOfCharOnLine
        member x.WordForward kind count = 
            let start = x.StartPoint
            let endPoint = TssUtil.FindNextWordStart start count kind  
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.WordBackward kind count =
            let start = x.StartPoint
            let startPoint = TssUtil.FindPreviousWordStart start count kind
            let span = SnapshotSpan(startPoint,start)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.AllWord kind count = 
            let start = x.StartPoint
            let start = match TssUtil.FindCurrentFullWordSpan start kind with 
                            | Some (s) -> s.Start
                            | None -> start
            let endPoint = TssUtil.FindNextWordStart start count kind  
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.EndOfWord kind count = 
            let start = x.StartPoint
            let tss = SnapshotPointUtil.GetSnapshot start
            let snapshotEnd = SnapshotUtil.GetEndPoint tss
            let rec inner start count = 
                if count <= 0 || start = snapshotEnd then start
                else
    
                    // Move start to the first word if we're currently on whitespace
                    let start = 
                        if System.Char.IsWhiteSpace(start.GetChar()) then TssUtil.FindNextWordStart start 1 kind 
                        else start
    
                    if start = snapshotEnd then snapshotEnd
                    else
                        // Get the span of the current word and the end completes the motion
                        match TssUtil.FindCurrentFullWordSpan start kind with
                        | None -> SnapshotUtil.GetEndPoint start.Snapshot
                        | Some(s) -> inner s.End (count-1)
    
            let endPoint = inner start count
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.EndOfLine count = 
            let start = x.StartPoint
            let span = SnapshotPointUtil.GetLineRangeSpan start count
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.FirstNonWhitespaceOnLine () =
            let start = x.StartPoint
            let line = start.GetContainingLine()
            let found = SnapshotLineUtil.GetPoints line
                            |> Seq.filter (fun x -> x.Position < start.Position)
                            |> Seq.tryFind (fun x-> x.GetChar() <> ' ')
            let span = match found with 
                        | Some p -> new SnapshotSpan(p, start)
                        | None -> new SnapshotSpan(start,0)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} 
        member x.BeginingOfLine () =
            let start = x.StartPoint
            let line = SnapshotPointUtil.GetContainingLine start
            let span = SnapshotSpan(line.Start, start)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.LineDownToFirstNonWhitespace count =
            let start = x.StartPoint
            let line = SnapshotPointUtil.GetContainingLine start
            let number = line.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
            let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
            let span = SnapshotSpan(start, point.Add(1)) // Add 1 since it's inclusive
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}
        member x.LineUpToFirstNonWhitespace count =
            let point = x.StartPoint
            let endLine = SnapshotPointUtil.GetContainingLine point
            let startLine = SnapshotUtil.GetLineOrFirst endLine.Snapshot (endLine.LineNumber - count)
            let span = SnapshotSpan(startLine.Start, endLine.End)
            let column = TssUtil.FindFirstNonWhitespaceCharacter startLine
            {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column= Some column.Position} 
        member x.CharLeft count = 
            let start = x.StartPoint
            let prev = SnapshotPointUtil.GetPreviousPointOnLine start count 
            if prev = start then None
            else {Span=SnapshotSpan(prev,start); IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some
        member x.CharRight count =
            let start = x.StartPoint
            let next = SnapshotPointUtil.GetNextPointOnLine start count 
            if next = start then None
            else {Span=SnapshotSpan(start,next); IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None } |> Some
        member x.LineUp count =     
            let point = x.StartPoint
            let endLine = SnapshotPointUtil.GetContainingLine point
            let startLineNumber = max 0 (endLine.LineNumber - count)
            let startLine = SnapshotUtil.GetLine endLine.Snapshot startLineNumber
            let span = SnapshotSpan(startLine.Start, endLine.End)
            {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } 
        member x.LineDown count = 
            let point = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine point
            let endLineNumber = startLine.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast startLine.Snapshot endLineNumber
            let span = SnapshotSpan(startLine.Start, endLine.End)            
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } 
        member x.LineOrFirstToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrFirst tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetFirstLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LineOrLastToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrLast tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetFirstLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LastNonWhitespaceOnLine count = 
            let start = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine start
            let snapshot = startLine.Snapshot
            let number = startLine.LineNumber + (count-1)
            let endLine = SnapshotUtil.GetLineOrLast snapshot number
            let endPoint = TssUtil.FindLastNonWhitespaceCharacter endLine
            let endPoint = if SnapshotUtil.GetEndPoint snapshot = endPoint then endPoint else endPoint.Add(1)
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}


