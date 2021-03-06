﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class MotionUtilTest
    {
        static string[] s_lines = new string[]
            {
                "summary description for this line",
                "some other line",
                "running out of things to make up"
            };

        private ITextBuffer _buffer;
        private ITextView _textView;
        private IVimGlobalSettings _settings;
        private MotionUtil _utilRaw;
        private IMotionUtil _util;

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
        }

        public void Create(params string[] lines)
        {
            _textView = Utils.EditorUtil.CreateView(lines);
            _buffer = _textView.TextBuffer;
            _settings = new Vim.GlobalSettings();
            _utilRaw = new MotionUtil(_textView, _settings);
            _util = _utilRaw;
        }

        [Test]
        public void WordForward1()
        {
            Create("foo bar");
            _textView.MoveCaretTo(0);
            var res = _util.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(4, span.Length);
            Assert.AreEqual("foo ", span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void WordForward2()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var res = _util.WordForward(WordKind.NormalWord, 1);
            var span = res.Span;
            Assert.AreEqual(3, span.Length);
            Assert.AreEqual("oo ", span.GetText());
        }

        [Test, Description("Word motion with a count")]
        public void WordForward3()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(0);
            var res = _util.WordForward(WordKind.NormalWord, 2);
            Assert.AreEqual("foo bar ", res.Span.GetText());
        }

        [Test, Description("Count across lines")]
        public void WordForward4()
        {
            Create("foo bar", "baz jaz");
            var res = _util.WordForward(WordKind.NormalWord, 3);
            Assert.AreEqual("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
        }

        [Test, Description("Count off the end of the buffer")]
        public void WordForward5()
        {
            Create("foo bar");
            var res = _util.WordForward(WordKind.NormalWord, 10);
            Assert.AreEqual("foo bar", res.Span.GetText());
        }

        [Test]
        public void EndOfLine1()
        {
            Create("foo bar", "baz");
            var res = _util.EndOfLine(1);
            var span = res.Span;
            Assert.AreEqual("foo bar", span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine2()
        {
            Create("foo bar", "baz");
            _textView.MoveCaretTo(1);
            var res = _util.EndOfLine(1);
            Assert.AreEqual("oo bar", res.Span.GetText());
        }

        [Test]
        public void EndOfLine3()
        {
            Create("foo", "bar", "baz");
            var res = _util.EndOfLine(2);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", res.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfLine4()
        {
            Create("foo", "bar", "baz", "jar");
            var res = _util.EndOfLine(3);
            var tuple = res;
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine +"baz", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Inclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test,Description("Make sure counts past the end of the buffer don't crash")]
        public void EndOfLine5()
        {
            Create("foo");
            var res = _util.EndOfLine(300);
            Assert.AreEqual("foo", res.Span.GetText());
        }

        [Test]
        public void BeginingOfLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 1), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void BeginingOfLine2()
        {
            Create("foo");
            _textView.MoveCaretTo(2);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 2), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        [Description("Go to begining even if there is whitespace")]
        public void BeginingOfLine3()
        {
            Create("  foo");
            _textView.MoveCaretTo(4);
            var data = _util.BeginingOfLine();
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 4), data.OperationSpan);
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.IsFalse(data.IsForward);
        }

        [Test]
        public void FirstNonWhitespaceOnLine1()
        {
            Create("foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test, Description("Make sure it goes to the first non-whitespace character")]
        public void FirstNonWhitespaceOnLine2()
        {
            Create("  foo");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(0).End);
            var tuple = _util.FirstNonWhitespaceOnLine();
            Assert.AreEqual("foo", tuple.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, tuple.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, tuple.OperationKind);
        }

        [Test]
        public void AllWord1()
        {
            Create("foo bar");
            var data = _util.AllWord(WordKind.NormalWord, 1);
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        [Test]
        public void AllWord2()
        {
            Create("foo bar");
            _textView.MoveCaretTo(1);
            var data = _util.AllWord(WordKind.NormalWord, 1);
            Assert.AreEqual("foo ", data.Span.GetText());
        }

        [Test]
        public void AllWord3()
        {
            Create("foo bar baz");
            _textView.MoveCaretTo(1);
            var data = _util.AllWord(WordKind.NormalWord, 2);
            Assert.AreEqual("foo bar ", data.Span.GetText());
        }

        [Test]
        public void CharLeft1()
        {
            Create("foo bar");
            _textView.MoveCaretTo(2);
            var data = _util.CharLeft(2);
            Assert.IsTrue(data.IsSome());
            Assert.AreEqual("fo", data.Value.Span.GetText());
        }

        [Test]
        public void CharRight1()
        {
            Create("foo");
            var data = _util.CharRight(1);
            Assert.AreEqual("f", data.Value.Span.GetText());
            Assert.AreEqual(OperationKind.CharacterWise, data.Value.OperationKind);
            Assert.AreEqual(MotionKind.Exclusive, data.Value.MotionKind);
        }

        [Test]
        public void LineUp1()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(_buffer.GetLineFromLineNumber(1).Start);
            var data = _util.LineUp(1);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", data.Span.GetText());
        }

        [Test]
        public void EndOfWord1()
        {
            Create("foo bar");
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, 3), res.Span);
        }

        [Test, Description("Needs to cross the end of the line")]
        public void EndOfWord2()
        {
            Create("foo   ","bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(1).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void EndOfWord3()
        {
            Create("foo bar baz jaz");
            var res = _util.EndOfWord(WordKind.NormalWord, 2);
            var span = new SnapshotSpan(_buffer.CurrentSnapshot, 0, 7);
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Work across blank lines")]
        public void EndOfWord4()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 1);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                _buffer.GetLineFromLineNumber(2).Start.Add(3));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test, Description("Go off the end of the buffer")]
        public void EndOfWord5()
        {
            Create("foo   ", "", "bar");
            _textView.MoveCaretTo(4);
            var res = _util.EndOfWord(WordKind.NormalWord, 400);
            var span = new SnapshotSpan(
                _buffer.GetPoint(4),
                SnapshotUtil.GetEndPoint(_buffer.CurrentSnapshot));
            Assert.AreEqual(span, res.Span);
            Assert.AreEqual(MotionKind.Inclusive, res.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, res.OperationKind);
        }

        [Test]
        public void ForwardChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _util.ForwardChar('o', 1).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo", _util.ForwardChar('o', 1).Value.Span.GetText());
            _textView.MoveCaretTo(1);
            Assert.AreEqual("oo b", _util.ForwardChar('b', 1).Value.Span.GetText());
        }

        [Test]
        public void ForwardChar2()
        {
            Create("foo bar baz");
            var data = _util.ForwardChar('q', 1);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardChar3()
        {
            Create("foo bar baz");
            var data = _util.ForwardChar('o', 1).Value;
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test,Description("Bad count gets nothing in gVim")]
        public void ForwardChar4()
        {
            Create("foo bar baz");
            var data = _util.ForwardChar('o', 300);
            Assert.IsTrue(data.IsNone());
        }

        [Test]
        public void ForwardTillChar1()
        {
            Create("foo bar baz");
            Assert.AreEqual("f", _util.ForwardTillChar('o', 1).Value.Span.GetText());
            Assert.AreEqual("foo ", _util.ForwardTillChar('b', 1).Value.Span.GetText());
        }

        [Test]
        public void ForwardTillChar2()
        {
            Create("foo bar baz");
            Assert.IsTrue(_util.ForwardTillChar('q', 1).IsNone());
        }

        [Test]
        public void ForwardTillChar3()
        {
            Create("foo bar baz");
            Assert.AreEqual("fo", _util.ForwardTillChar('o', 2).Value.Span.GetText());
        }

        [Test,Description("Bad count gets nothing in gVim")]
        public void ForwardTillChar4()
        {
            Create("foo bar baz");
            Assert.IsTrue(_util.ForwardTillChar('o', 300).IsNone());
        }

        [Test]
        public void BackwardCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.BackwardChar('b', 1).Value; 
            Assert.AreEqual("ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.BackwardChar('b', 2).Value; 
            Assert.AreEqual("boy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion1()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.BackwardTillChar('b', 1).Value; 
            Assert.AreEqual("all", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void BackwardTillCharMotion2()
        {
            Create("the boy kicked the ball");
            _textView.MoveCaretTo(_buffer.GetLine(0).End);
            var data = _util.BackwardTillChar('b', 2).Value; 
            Assert.AreEqual("oy kicked the ball", data.Span.GetText());
            Assert.AreEqual(MotionKind.Exclusive, data.MotionKind);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace1()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineSpan(0, 1), data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace2()
        {
            Create("foo", "bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineSpan(0, 1), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace3()
        {
            Create("foo", "  bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineSpan(0, 1), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(2, data.Column.Value);
        }

        [Test]
        public void LineOrFirstToFirstNonWhitespace4()
        {
            Create("foo", "  bar", "baz");
            var data = _util.LineOrFirstToFirstNonWhitespace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineSpan(0, 0), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LineOrLastToFirstNonWhitespace1()
        {
            Create("foo", "bar", "baz");
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(2));
            Assert.AreEqual(_buffer.GetLineSpan(0, 1), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }        

        [Test]
        public void LineOrLastToFirstNonWhitespace2()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(0));
            Assert.AreEqual(_buffer.GetLineSpan(0, 1), data.Span);
            Assert.IsFalse(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }        

        [Test]
        public void LineOrLastToFirstNonWhitespace3()
        {
            Create("foo", "bar", "baz");
            _textView.MoveCaretTo(_buffer.GetLine(1).Start);
            var data = _util.LineOrLastToFirstNonWhitespace(FSharpOption.Create(500));
            Assert.AreEqual(_buffer.GetLineSpan(1, 2), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.LineWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
            Assert.AreEqual(0, data.Column.Value);
        }

        [Test]
        public void LastNonWhitespaceOnLine1()
        {
            Create("foo", "bar ");
            var data = _util.LastNonWhitespaceOnLine(1);
            Assert.AreEqual(_buffer.GetLineSpan(0), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhitespaceOnLine2()
        {
            Create("foo", "bar ","jaz");
            var data = _util.LastNonWhitespaceOnLine(2);
            Assert.AreEqual(new SnapshotSpan(_buffer.GetPoint(0), _buffer.GetLine(1).Start.Add(3)), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }

        [Test]
        public void LastNonWhitespaceOnLine3()
        {
            Create("foo", "bar ","jaz","");
            var data = _util.LastNonWhitespaceOnLine(300);
            Assert.AreEqual(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length), data.Span);
            Assert.IsTrue(data.IsForward);
            Assert.AreEqual(OperationKind.CharacterWise, data.OperationKind);
            Assert.AreEqual(MotionKind.Inclusive, data.MotionKind);
        }


    }   
    
}
