using NUnit.Framework;
using Starquill.Data.Art;

namespace Starquill.Tests.Art
{
    public class CsvTableTests
    {
        [Test]
        public void Parse_ReadsHeaderAndRows()
        {
            var t = CsvTable.Parse("a,b\n1,2\n3,4\n");
            CollectionAssert.AreEqual(new[] { "a", "b" }, t.Header);
            Assert.AreEqual(2, t.Rows.Count);
            Assert.AreEqual("4", t.Rows[1][1]);
        }

        [Test]
        public void Parse_HandlesCrlfBomAndBlankLines()
        {
            var t = CsvTable.Parse("﻿a,b\r\n1,2\r\n\r\n3,4");
            CollectionAssert.AreEqual(new[] { "a", "b" }, t.Header);
            Assert.AreEqual(2, t.Rows.Count);
            Assert.AreEqual("3", t.Rows[1][0]);
        }

        [Test]
        public void Parse_QuotedFieldsWithCommaQuoteAndNewline()
        {
            var t = CsvTable.Parse("a,b\n\"x, y\",\"say \"\"hi\"\"\"\n\"two\nlines\",z\n");
            Assert.AreEqual("x, y", t.Rows[0][0]);
            Assert.AreEqual("say \"hi\"", t.Rows[0][1]);
            Assert.AreEqual("two\nlines", t.Rows[1][0]);
        }

        [Test]
        public void Parse_PadsShortRowsAndRecordsLineNumbers()
        {
            var t = CsvTable.Parse("a,b,c\n1\n\n2,3,4\n");
            CollectionAssert.AreEqual(new[] { "1", "", "" }, t.Rows[0]);
            Assert.AreEqual(2, t.RowLines[0]);
            Assert.AreEqual(4, t.RowLines[1]);
        }

        [Test]
        public void Column_FindsHeaderIndexOrMinusOne()
        {
            var t = CsvTable.Parse("item_type,description\n");
            Assert.AreEqual(1, t.Column("description"));
            Assert.AreEqual(-1, t.Column("missing"));
        }

        [Test]
        public void ToCsv_RoundTripsAndQuotesOnlyWhenNeeded()
        {
            string text = "a,b\nplain,\"x, y\"\n";
            Assert.AreEqual(text, CsvTable.Parse(text).ToCsv());
        }

        [Test]
        public void ToCsv_EditOneCell_ChangesOneLine()
        {
            var t = CsvTable.Parse("a,b\n1,2\n3,4\n5,6\n");
            t.Rows[1][1] = "40";
            Assert.AreEqual("a,b\n1,2\n3,40\n5,6\n", t.ToCsv());
        }
    }
}
