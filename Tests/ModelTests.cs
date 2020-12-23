using System;
using System.Collections.Generic;
using CronoSeries.TimeSeries.Data;
using CronoSeries.TimeSeries.Models;
using Microsoft.VisualBasic.FileIO;
using NUnit.Framework;
using static System.Console;

namespace Tests
{
    public class Tests
    {
        private List<DateTime> _dates;
        private TimeSeries _extData;


        [SetUp]
        public void Setup()
        {
            _dates = new List<DateTime>();
            for (var i = 1; i < 12; i++) _dates.Add(new DateTime(2015, 1, i));

            using var parser = new TextFieldParser("TestData/spy.csv")
            {
                TextFieldType = FieldType.Delimited
            };
            var dates = new List<DateTime>();
            var vals = new List<double>();

            // Note: could also use GetTSFromReader(TextReader sreader, bool ignoreDuplicates)

            parser.SetDelimiters(",");
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                var newDate = fields[0].Split("-");
                var date = new DateTime(Convert.ToInt32(newDate[0]),
                    Convert.ToInt32(newDate[1]),
                    Convert.ToInt32(newDate[2]));
                var val = Convert.ToDouble(fields[1]);
                dates.Add(date);
                vals.Add(val);
            }

            _extData = new TimeSeries();
            _extData.DataFromLists(dates, vals);
        }

        [Test]
        public void GarchSimulates()
        {
            var _garch = new GARCHModel(GARCHModel.GARCHType.Standard, 1, 1);
            var simulateData = _garch.SimulateData(_dates, 22);
            Assert.NotNull(simulateData);
            Assert.IsTrue(simulateData.Count > 0);
            Write($"Sample mean: {simulateData.SampleMean()}");
            _garch.TheData = simulateData; // Feed back in
            _garch.FitByMLE(200, 100, 0, null);
            Write(_garch.Description);
        }

        [Test]
        public void ArmaSimulates()
        {
            var _arma = new ARMAModel(1, 1);
            var simulateData = _arma.SimulateData(_dates, 22);
            Assert.NotNull(simulateData);
            Assert.IsTrue(simulateData.Count > 0);
            Write($"Sample mean: {simulateData.SampleMean()}");
            _arma.TheData = simulateData; // Feed back in
            _arma.FitByMLE(200, 100, 0, null);
            Write(_arma.Description);
        }

        [Test]
        public void ArmaFromExtData()
        {
            var arma = new ARMAModel(1, 1, _extData);
            Assert.IsTrue(arma.CanUseMLE());
            arma.FitByMLE(200, 100, 0, null);
            Write($"Sample mean: {_extData.SampleMean()}, From model: {arma.Mu} \n");
            Write($"Desc: {arma.Description}");
        }

        [Test]
        public void GarchFromExtData()
        {
            var garch = new GARCHModel(GARCHModel.GARCHType.EGARCH, 1, 1, _extData);
            Assert.IsTrue(garch.CanUseMLE());
            garch.FitByMLE(200, 100, 0, null);
            Write($"Sample mean: {_extData.SampleMean()} \n");
            Write($"Desc: {garch.Description}");
        }
    }
}