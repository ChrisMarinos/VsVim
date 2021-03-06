﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCore.Test.Utils;
using Moq;
using Vim.Extensions;
using Microsoft.FSharp.Core;

namespace VimCore.Test
{
    [TestFixture]
    public class MotionCaptureTest
    {
        private Mock<ITextView> _textView;
        private Mock<IMotionUtil> _util;
        private MotionCapture _captureRaw;
        private IMotionCapture _capture;

        [SetUp]
        public void Create()
        {
            _util = new Mock<IMotionUtil>(MockBehavior.Strict);
            _textView = Mock.MockObjectFactory.CreateTextView();
            _captureRaw = new MotionCapture(_textView.Object, _util.Object);
            _capture = _captureRaw;
        }

        internal MotionResult Process(string input, int? count)
        {
            var realCount = count.HasValue
                ? FSharpOption.Create(count.Value)
                : FSharpOption<int>.None;
            var res = _capture.GetMotion(
                InputUtil.CharToKeyInput(input[0]),
                realCount);
            foreach (var cur in input.Skip(1))
            {
                Assert.IsTrue(res.IsNeedMoreInput);
                var needMore = (MotionResult.NeedMoreInput)res;
                res = needMore.Item.Invoke(InputUtil.CharToKeyInput(cur));
            }

            return res;
        }

        internal void ProcessComplete(string input, int? count=null)
        {
            Assert.IsTrue(Process(input, count).IsComplete);
        }

        internal MotionData CreateMotionData()
        {
            var point = Mock.MockObjectFactory.CreateSnapshotPoint(42);
            return new MotionData(
                new SnapshotSpan(point, point),
                true,
                MotionKind.Inclusive,
                OperationKind.CharacterWise,
                FSharpOption.Create(42));
        }

        [Test]
        public void Word1()
        {
            _util
                .Setup(x => x.WordForward(WordKind.NormalWord, 1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("w", 1);
            _util.Verify();
        }

        [Test]
        public void Word2()
        {
            _util
                .Setup(x => x.WordForward(WordKind.NormalWord, 2))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("w", 2);
        }

        [Test]
        public void BadInput()
        {
            var res = Process("z", 1);
            Assert.IsTrue(res.IsError);
        }

        [Test]
        public void Motion_Dollar1()
        {
            _util
                .Setup(x => x.EndOfLine(1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("$", 1);
            _util.Verify();
        }

        [Test]
        public void Motion_Dollar2()
        {
            _util
                .Setup(x => x.EndOfLine(2))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("$", 2);
            _util.Verify();
        }

        [Test]
        public void Motion_End()
        {
            _util
                .Setup(x => x.EndOfLine(1))
                .Returns(CreateMotionData())
                .Verifiable();
            _capture.GetMotion(InputUtil.VimKeyToKeyInput(VimKey.EndKey), FSharpOption<int>.None);
            _util.Verify();
        }

        [Test]
        public void BeginingOfLine1()
        {
            _util
                .Setup(x => x.BeginingOfLine())
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("0", 1);
            _util.Verify();
        }

        [Test]
        public void FirstNonWhitespaceOnLine1()
        {
            _util
                .Setup(x => x.FirstNonWhitespaceOnLine())
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("^", 1);
            _util.Verify();
        }

        [Test]
        public void AllWord1()
        {
            _util
                .Setup(x => x.AllWord(WordKind.NormalWord, 1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("aw", 1);
            _util.Verify();
        }

        [Test]
        public void AllWord2()
        {
            _util
                .Setup(x => x.AllWord(WordKind.NormalWord, 2))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("aw", 2);
            _util.Verify();
        }

        [Test]
        public void CharLeft1()
        {
            _util
                .Setup(x => x.CharLeft(1))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("h",1);
            _util.Verify();
        }

        [Test]
        public void CharLeft2()
        {
            _util
                .Setup(x => x.CharLeft(2))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("2h",1);
            _util.Verify();
        }

        [Test]
        public void CharRight1()
        {
            _util
                .Setup(x => x.CharRight(2))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("2l",1);
            _util.Verify();
        }

        [Test]
        public void LineUp1()
        {
            _util
                .Setup(x => x.LineUp(1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("k", 1);
            _util.Verify();
        }

        [Test]
        public void EndOfWord1()
        {
            _util
                .Setup(x => x.EndOfWord(WordKind.NormalWord, 1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("e", 1);
            _util.Verify();
        }

        public void EndOfWord2()
        {
            _util
                .Setup(x => x.EndOfWord(WordKind.BigWord, 1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("E", 1);
            _util.Verify();
        }

        [Test]
        public void ForwardChar1()
        {
            _util
                .Setup(x => x.ForwardChar('c', 1))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("fc", 1);
            _util.Verify();
        }

        [Test]
        public void ForwardTillChar1()
        {
            _util
                .Setup(x => x.ForwardTillChar('c', 1))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("tc", 1);
            _util.Verify();
        }

        [Test]
        public void BackwardCharMotion1()
        {
            _util
                .Setup(x => x.BackwardChar('c', 1))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("Fc", 1);
            _util.Verify();
        }

        [Test]
        public void BackwardTillCharMotion1()
        {
            _util
                .Setup(x => x.BackwardTillChar('c', 1))
                .Returns(FSharpOption.Create(CreateMotionData()))
                .Verifiable();
            ProcessComplete("Tc", 1);
            _util.Verify();
        }

        [Test]
        public void Motion_G1()
        {
            _util
                .Setup(x => x.LineOrLastToFirstNonWhitespace( FSharpOption<int>.None))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("G");
            _util.Verify();
        }

        [Test]
        public void Motion_G2()
        {
            _util
                .Setup(x => x.LineOrLastToFirstNonWhitespace(FSharpOption.Create(1)))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("1G");
            _util.Verify();
        }

        [Test]
        public void Motion_G3()
        {
            _util
                .Setup(x => x.LineOrLastToFirstNonWhitespace( FSharpOption.Create(42)))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("42G");
            _util.Verify();
        }

        [Test]
        public void Motion_gg1()
        {
            _util
                .Setup(x => x.LineOrFirstToFirstNonWhitespace(FSharpOption<int>.None))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("gg");
            _util.Verify();
        }

        [Test]
        public void Motion_gg2()
        {
            _util
                .Setup(x => x.LineOrFirstToFirstNonWhitespace( FSharpOption.Create(2)))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("2gg");
            _util.Verify();
        }

        [Test]
        public void Motion_g_1()
        {
            _util
                .Setup(x => x.LastNonWhitespaceOnLine(1))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("g_");
            _util.Verify();
        }

        [Test]
        public void Motion_g_2()
        {
            _util
                .Setup(x => x.LastNonWhitespaceOnLine(2))
                .Returns(CreateMotionData())
                .Verifiable();
            ProcessComplete("2g_");
            _util.Verify();
        }
    }

}
