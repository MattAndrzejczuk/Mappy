﻿namespace MappyTest
{
    using System;

    using Mappy.Maybe;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MaybeTest
    {
        [TestMethod]
        public void TestFromValue()
        {
            var i = Maybe<string>.From("asdf");
            i.Match(
                some: x => Assert.AreEqual("asdf", x),
                none: Assert.Fail);
        }

        [TestMethod]
        public void TestFromNull()
        {
            var i = Maybe<string>.From(null);

            Assert.IsTrue(i.IsNone);
            Assert.IsFalse(i.IsSome);

            i.IfSome(_ => Assert.Fail());

            bool passed = false;
            i.IfNone(() => passed = true);
            Assert.IsTrue(passed);
        }

        [TestMethod]
        public void TestSome()
        {
            var i = Maybe<int>.Some(2);
            Assert.IsTrue(i.IsSome);
            Assert.IsFalse(i.IsNone);

            i.Match(
                some: x => Assert.AreEqual(2, x),
                none: Assert.Fail);
        }

        [TestMethod]
        public void TestSomeThrowsOnNull()
        {
            try
            {
                Maybe<string>.Some(null);
                Assert.Fail();
            }
            catch (ArgumentNullException)
            {
                // we passed
            }
        }

        [TestMethod]
        public void TestMap()
        {
            var i = Maybe<int>.Some(2);
            var j = i.Map(x => x.ToString());
            j.Match(
                some: x => Assert.AreEqual("2", x),
                none: Assert.Fail);
        }

        [TestMethod]
        public void TestMapNull()
        {
            var i = Maybe<int>.None;
            var j = i.Map(x => x.ToString());

            bool passed = false;
            j.Match(
                some: x => Assert.Fail(),
                none: () => passed = true);
            Assert.IsTrue(passed);
        }

        [TestMethod]
        public void TestWhereFalse()
        {
            var i = Maybe<int>.Some(1);
            var j = i.Where(_ => false);

            Assert.IsTrue(j.IsNone);
        }

        [TestMethod]
        public void TestWhereTrue()
        {
            var i = Maybe<int>.Some(1);
            var j = i.Where(_ => true);

            j.Match(
                some: x => Assert.AreEqual(1, x),
                none: Assert.Fail);
        }

        [TestMethod]
        public void TestWhereNull()
        {
            var i = Maybe<int>.None;
            var j = i.Where(_ => true);

            Assert.IsTrue(j.IsNone);
        }

        [TestMethod]
        public void TestOrValueNone()
        {
            var i = Maybe<int>.None;
            var j = i.Or(23);
            Assert.AreEqual(23, j);
        }

        [TestMethod]
        public void TestOrValueSome()
        {
            var i = Maybe<int>.Some(21);
            var j = i.Or(23);
            Assert.AreEqual(21, j);
        }
    }
}
