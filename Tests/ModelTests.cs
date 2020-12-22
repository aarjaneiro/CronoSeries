using System;
using System.Collections.Generic;
using CronoSeries.TimeSeries.Data;
using NUnit.Framework;
using CronoSeries.TimeSeries.Models;
using Microsoft.VisualBasic.FileIO;
using static System.Console;

namespace Tests
{
    public class Tests
    {
        private GARCHModel _garch;
        private ARMAModel _arma;
        private List<DateTime> _dates;
        private TimeSeries _extData;


        [SetUp]
        public void Setup()
        {
            _dates = new List<DateTime>();
            for (int i = 1; i < 12; i++)
            {
                _dates.Add(new DateTime(2015, 1, i));
            }

            using TextFieldParser parser = new TextFieldParser("TestData/spy.csv")
            {
                TextFieldType = FieldType.Delimited
            };
            _extData = new TimeSeries();
            parser.SetDelimiters(",");
            while (!parser.EndOfData)
            {
                string[] fields = parser.ReadFields();
                var newDate = fields[0].Split("-");
                var date = new DateTime(Convert.ToInt32(newDate[0]),
                    Convert.ToInt32(newDate[1]),
                    Convert.ToInt32(newDate[2]));
                var val = (Convert.ToDouble(fields[1]));
                _extData.Add(date, val, false);
            }
        }

        [Test]
        public void GarchSimulates()
        {
            _garch = new GARCHModel(GARCHModel.GARCHType.Standard, 1, 1);
            var simulateData = _garch.SimulateData(_dates, 22);
            Assert.NotNull(simulateData);
            Assert.IsTrue(simulateData.Count > 0);
            Write(simulateData.Description);
            _garch.TheData = simulateData; // Feed back in
            _garch.FitByMLE(200, 100, 0, null);
            Write(_garch.Description);
        }

        [Test]
        public void ArmaSimulates()
        {
            _arma = new ARMAModel(1, 1);
            var simulateData = _arma.SimulateData(_dates, 22);
            Assert.NotNull(simulateData);
            Assert.IsTrue(simulateData.Count > 0);
            Write(simulateData.Description);
            _arma.TheData = simulateData; // Feed back in
            _arma.FitByMLE(200, 100, 0, null);
            Write(_arma.Description);
        }

        // Todo 
        /*[Test]
        public void ArmaExtData()
        {
            _arma = new ARMAModel(1,1);
              _arma.theData = _extData;
            WriteLine(_extData.Description);
              _arma.FitByMLE(200, 100, 0, null);
            var des =   _arma.Description;
            Assert.IsNotNull(des);
            Write(des);
        }*/
    }
}