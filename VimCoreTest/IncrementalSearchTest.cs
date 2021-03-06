﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using VimCore.Test.Utils;
using Vim.Modes.Normal;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using VimCore.Test.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class IncrementalSearchTest
    {
        private static SearchOptions s_options = SearchOptions.AllowIgnoreCase | SearchOptions.AllowSmartCase;
        private MockFactory _factory;
        private Mock<ISearchService> _searchService;
        private Mock<ITextStructureNavigator> _nav;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IOutliningManager> _outlining;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _searchService = _factory.Create<ISearchService>();
            _nav = _factory.Create<ITextStructureNavigator>();
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _outlining.Setup(x => x.ExpandAll(It.IsAny<SnapshotSpan>(), It.IsAny<Predicate<ICollapsed>>())).Returns<IEnumerable<ICollapsed>>(null);
            _searchRaw = new IncrementalSearch(
                _textView,
                _outlining.Object,
                _settings.Object,
                _nav.Object,
                _searchService.Object);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            _search.Begin(SearchKind.ForwardWithWrap);
            foreach (var cur in value)
            {
                var ki = InputUtil.CharToKeyInput(cur);
                Assert.IsTrue(_search.Process(ki).IsSearchNeedMore);
            }
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey)).IsSearchComplete);
        }

        [Test]
        public void Process1()
        {
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("b"), SearchKind.ForwardWithWrap, s_options);
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNext(data, _textView.GetCaretPoint(), _nav.Object))
                .Returns(FSharpOption.Create(_textView.GetLineSpan(0)));
            Assert.IsTrue(_search.Process(InputUtil.CharToKeyInput('b')).IsSearchNeedMore);
        }

        [Test]
        public void Process2()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern(""), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey)).IsSearchComplete);
            _searchService.Verify();
        }

        [Test]
        public void Process3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey)).IsSearchCancelled);
        }

        [Test]
        public void LastSearch1()
        {
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options);
            _searchService.SetupSet(x => x.LastSearch = data).Verifiable();
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            ProcessWithEnter("foo");
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();

            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern("foo bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("foo bar");
            _factory.Verify();

            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern("bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("bar");
            _factory.Verify();
        }

        [Test]
        public void Status1()
        {
            Create("foo");
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    didRun = true;
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status2()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    Assert.AreEqual("a", tuple.Item1.Text.RawText);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };
            _search.Process(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status3()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchCompleted += (unused, tuple) =>
                {
                    Assert.AreEqual("foo", tuple.Item1.Text.RawText);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            Assert.AreEqual("B", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void CurrentSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('B'));
            _factory.Verify();
            Assert.AreEqual("B", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.CharToKeyInput('a'));
            _search.Process(InputUtil.CharToKeyInput('b'));
        }


        [Test]
        public void InSearch1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            Assert.IsTrue(_search.InSearch);
        }

        [Test]
        public void InSearch2()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.AllowSmartCase | SearchOptions.AllowIgnoreCase));
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.IsSome());
        }

        [Test, Description("Cancelling needs to remove the CurrentSearch")]
        public void InSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.IsFalse(_search.InSearch);
        }

        [Test, Description("Backspace with blank search query cancels search")]
        public void Backspace1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            var result = _search.Process(InputUtil.VimKeyToKeyInput(VimKey.BackKey));
            Assert.IsTrue(result.IsSearchCancelled);
        }

        [Test, Description("Backspace with text doesn't crash")]
        public void Backspace2()
        {
            Create("foo bar");
            _searchService.SetupSet(x => x.LastSearch = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.None));
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Forward);
            _search.Process(InputUtil.CharToKeyInput('b'));
            var result = _search.Process(InputUtil.VimKeyToKeyInput(VimKey.BackKey));
            Assert.IsTrue(result.IsSearchNeedMore);
            _searchService.Verify();
        }


    }
}