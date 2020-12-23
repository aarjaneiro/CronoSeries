/*
Derived from the Cronos Package, http://www.codeplex.com/cronos
Copyright (C) 2009 Anthony Brockwell

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
*/

using System;
using System.Collections.Generic;

namespace CronoSeries.TimeSeries.Data
{
    [Serializable]
    public class TimeSeriesBase<T>
    {
        //public static readonly double ticksPerDay = new TimeSpan(24, 0, 0).Ticks;

        public TimeSeriesBase()
        {
            Values = new List<T>();
            Times = new List<DateTime>();
            Title = "";
            Description = "";
        }

        /// <summary>
        ///     A list of DateTime values for each data point.
        /// </summary>
        public List<DateTime> Times { protected set; get; }

        /// <summary>
        ///     A list of data corresponding to each DateTime point in <see cref="Times" />.
        /// </summary>
        public List<T> Values { protected set; get; }

        public int Dimension { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        public int Count => Values.Count;

        public T this[int i] => Values[i];

        public DateTime TimeStamp(int idx)
        {
            return Times[idx];
        }

        public int IndexAtOrBefore(DateTime time, out bool exact)
        {
            exact = false;
            var sidx = Times.BinarySearch(time);
            if (sidx < 0)
                sidx = ~sidx - 1;
            else
                exact = true;
            return sidx;
        }

        /// <summary>
        ///     inserts a new point into the time series.
        ///     if the datetime already has a value, then it either replaces it
        ///     or throws an exception
        /// </summary>
        /// <param name="time">time at which value is to be inserted</param>
        /// <param name="value">value to be inserted</param>
        /// <param name="forceOverwrite">determines whether exception is thrown when point alread exists</param>
        public void InsertInMiddle(DateTime time, T value, bool forceOverwrite)
        {
            if (Count == 0)
            {
                Add(time, value, forceOverwrite);
                return;
            }

            var sidx = Times.BinarySearch(time);
            if (sidx >= 0) // got a match
            {
                if (forceOverwrite)
                {
                    Values[sidx] = value;
                    return;
                }

                throw new ApplicationException("Cannot insert over the top of an existing point.");
            }

            var i0 = ~sidx - 1; // the index of the time before the searched time
            if (i0 == -1) // it goes at the front
            {
                Values.Insert(0, value);
                Times.Insert(0, time);
            }
            else if (i0 == Count - 1) // it goes at the end
            {
                Add(time, value, forceOverwrite);
            }
            else // it goes in the middle somewhere
            {
                Values.Insert(i0 + 1, value);
                Times.Insert(i0 + 1, time);
            }
        }

        public TimeSeriesBase<T> GetSubrange(DateTime t0, DateTime t1)
        {
            var retval = new TimeSeriesBase<T>();
            bool gotIt0;
            bool gotIt1;
            var i0 = IndexAtOrBefore(t0, out gotIt0);
            var i1 = IndexAtOrBefore(t1, out gotIt1);
            if (i1 == -1) // there is nothing at or before t1
                return retval; // empty
            if (!gotIt0)
                ++i0;

            for (var t = i0; t <= i1; ++t)
                retval.Add(TimeStamp(t), Values[t], false);
            return retval;
        }

        /// <summary>
        ///     does a binary search for the appropriate index, interpolating using a step-function.
        ///     if earlier or later than first or last observation, the first/last value is returned.
        /// </summary>
        public T ValueAtTime(DateTime time)
        {
            bool gotIt;
            return ValueAtTime(time, out gotIt);
        }

        /// <summary>
        ///     does a binary search for the appropriate index, interpolating using a step-function.
        ///     if earlier or later than first or last observation, the first/last value is returned.
        ///     this function sets an output flag indicating whether or not there was an exact time match.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="gotExactTime"></param>
        /// <returns></returns>
        public T ValueAtTime(DateTime time, out bool gotExactTime)
        {
            if (Count == 0) // default: return 0 if its an empty TS
            {
                gotExactTime = false;
                return default;
            }

            var sidx = Times.BinarySearch(time);
            if (sidx >= 0)
            {
                gotExactTime = true;
                return Values[sidx];
            }

            var justBefore = ~sidx - 1;
            gotExactTime = false;

            if (justBefore == -1)
                return Values[0];

            return Values[justBefore];
        }


        public void DeletePoint(DateTime time)
        {
            if (Count == 0)
                throw new ApplicationException("Cannot delete point that does not exist.");

            var sidx = Times.BinarySearch(time);
            if (sidx < 0)
                throw new ApplicationException("Cannot delete point that does not exist.");

            Values.RemoveAt(sidx);
            Times.RemoveAt(sidx);
        }

        /// <summary>
        ///     removes all points strictly before the specified time
        /// </summary>
        /// <param name="time"></param>
        /// <param name="keepOne">true if one extra point should be kept</param>
        public void DeleteBefore(DateTime time, bool keepOne)
        {
            if (Count == 0)
                return;
            var sidx = Times.BinarySearch(time);
            if (sidx < 0) // not found
            {
                sidx = ~sidx - 1; // time just before the specified time
                if (keepOne)
                    --sidx;
            }
            else
            {
                --sidx;
                if (keepOne)
                    --sidx;
            }

            if (sidx >= 0) // there is at least something to delete
            {
                Values.RemoveRange(0, sidx + 1);
                Times.RemoveRange(0, sidx + 1);
            }
        }

        public void SetValue(int idx, T value)
        {
            Values[idx] = value;
        }

        public DateTime GetFirstTime()
        {
            if (Times.Count > 0)
                return Times[0];
            return DateTime.MaxValue;
        }

        public DateTime GetLastTime()
        {
            if (Times.Count > 0)
                return Times[Times.Count - 1];
            return DateTime.MinValue; // return something arbitrary if it's empty
        }

        public TimeSpan GetCommonSamplingInterval()
        {
            var counts = new Dictionary<TimeSpan, int>(50);
            for (var t = Times.Count - 1; t > 0 && t > Times.Count - 100; --t)
            {
                var interval = Times[t] - Times[t - 1];
                if (counts.ContainsKey(interval))
                    counts[interval] = counts[interval] + 1;
                else
                    counts.Add(interval, 1);
            }

            var bestCount = 0;
            var bestInterval = new TimeSpan(1, 0, 0, 0);
            foreach (var x in counts)
                if (x.Value > bestCount)
                {
                    bestCount = x.Value;
                    bestInterval = x.Key;
                }

            return bestInterval;
        }

        /// <summary>
        ///     adds a whole additional time series to the end of this one
        ///     (actually it doesn't have to be at the end, but it's most efficient that way)
        /// </summary>
        /// <param name="otherTS"></param>
        /// <param name="forceOverwrite"></param>
        public void Add(TimeSeriesBase<T> otherTS, bool forceOverwrite)
        {
            for (var t = 0; t < otherTS.Count; ++t)
                Add(otherTS.TimeStamp(t), otherTS[t], forceOverwrite);
        }

        /// <summary>
        ///     adds a value to the time series.
        ///     works most efficiently if it comes at the end chronologically.
        ///     if the timestamp already exists, then it either throws an exception
        ///     or overwrites the existing value.
        /// </summary>
        /// <param name="time">time of new point</param>
        /// <param name="value">value to add</param>
        /// <param name="forceOverwrite">
        ///     if timestamp already exists, determines whether to throw an exception or overwrite
        ///     existing value
        /// </param>
        public void Add(DateTime time, T value, bool forceOverwrite)
        {
            // do a little check
            var comesAtEnd = true;
            if (Times.Count > 0)
            {
                var lastTime = Times[Times.Count - 1];
                if (time <= lastTime)
                    comesAtEnd = false;
            }

            if (comesAtEnd)
            {
                Times.Add(time);
                Values.Add(value);
            }
            else
            {
                InsertInMiddle(time, value, forceOverwrite);
            }
        }

        public void RemoveFirst()
        {
            if (Count <= 0) return;
            Values.RemoveAt(0);
            Times.RemoveAt(0);
        }

        public void RemoveLast()
        {
            if (Count <= 0) return;
            Values.RemoveAt(Values.Count - 1);
            Times.RemoveAt(Times.Count - 1);
        }
    }
}