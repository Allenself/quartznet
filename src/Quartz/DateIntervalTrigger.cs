#region License
/* 
 * Copyright 2001-2009 Terracotta, Inc. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */
#endregion

using System;

namespace Quartz
{
    /// <summary>
    ///  A concrete <see cref="Trigger" /> that is used to fire a <see cref="JobDetail" />
    ///  based upon repeating calendar time intervals.
    ///  </summary>
    /// <remarks>
    /// The trigger will fire every N (see <see cref="RepeatInterval" />) units of calendar time
    /// (see <see cref="RepeatIntervalUnit" />) as specified in the trigger's definition.  
    /// This trigger can achieve schedules that are not possible with <see cref="SimpleTrigger" /> (e.g 
    /// because months are not a fixed number of seconds) or <see cref="CronTrigger" /> (e.g. because
    /// "every 5 months" is not an even divisor of 12).
    /// <p>
    /// If you use an interval unit of <see cref="IntervalUnit.Month" /> then care should be taken when setting
    /// a <code>startTime</code> value that is on a day near the end of the month.  For example,
    /// if you choose a start time that occurs on January 31st, and have a trigger with unit
    /// <see cref="IntervalUnit.Month" /> and interval <code>1</code>, then the next fire time will be February 28th, 
    /// and the next time after that will be March 28th - and essentially each subsequent firing will 
    /// occur on the 28th of the month, even if a 31st day exists.  If you want a trigger that always
    /// fires on the last day of the month - regardless of the number of days in the month, 
    /// you should use <see cref="CronTrigger" />.
    /// </p> 
    /// </remarks>
    /// <see cref="Trigger" />
    /// <see cref="CronTrigger" />
    /// <see cref="SimpleTrigger" />
    /// <see cref="NthIncludedDayTrigger" />
    /// <see cref="TriggerUtils" />
    /// <since>1.2</since>
    /// <author>James House</author>
    /// <author>Marko Lahma (.NET)</author>
    public class DateIntervalTrigger : Trigger
    {
        private const int YearToGiveupSchedulingAt = 2299;

        public enum IntervalUnit
        {
            Second,
            Minute,
            Hour,
            Day,
            Week,
            Month,
            Year
        } ;

        private DateTime startTime;
        private DateTime? endTime;
        private DateTime? nextFireTime;
        private DateTime? previousFireTime;
        private int repeatInterval;
        private IntervalUnit repeatIntervalUnit = IntervalUnit.Day;
        private int timesTriggered;
        private bool complete;

        /// <summary>
        /// Create a <code>DateIntervalTrigger</code> with no settings.
        /// </summary>
        public DateIntervalTrigger()
        {
        }

        /// <summary>
        /// Create a <see cref="DateIntervalTrigger" /> that will occur immediately, and
        /// repeat at the the given interval.
        /// </summary>
        /// <param name="name">Name for the trigger instance.</param>
        /// <param name="intervalUnit">The repeat interval unit (minutes, days, months, etc).</param>
        /// <param name="repeatInterval">The number of milliseconds to pause between the repeat firing.</param>
        public DateIntervalTrigger(string name, IntervalUnit intervalUnit, int repeatInterval)
            : this(name, null, intervalUnit, repeatInterval)
        {
        }

        /// <summary>
        /// Create a <see cref="DateIntervalTrigger" /> that will occur immediately, and
        /// repeat at the the given interval
        /// </summary>
        /// <param name="name">Name for the trigger instance.</param>
        /// <param name="group">Group for the trigger instance.</param>
        /// <param name="intervalUnit">The repeat interval unit (minutes, days, months, etc).</param>
        /// <param name="repeatInterval">The number of milliseconds to pause between the repeat firing.</param>
        public DateIntervalTrigger(string name, string group, IntervalUnit intervalUnit,
                                   int repeatInterval)
            : this(name, group, SystemTime.UtcNow(), null, intervalUnit, repeatInterval)
        {
        }

        /// <summary>
        /// Create a <see cref="DateIntervalTrigger" /> that will occur at the given time,
        /// and repeat at the the given interval until the given end time.
        /// </summary>
        /// <param name="name">Name for the trigger instance.</param>
        /// <param name="startTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to fire.</param>
        /// <param name="endTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to quit repeat firing.</param>
        /// <param name="intervalUnit">The repeat interval unit (minutes, days, months, etc).</param>
        /// <param name="repeatInterval">The number of milliseconds to pause between the repeat firing.</param>
        public DateIntervalTrigger(string name, DateTime startTimeUtc,
                                   DateTime? endTimeUtc, IntervalUnit intervalUnit, int repeatInterval)
            : this(name, null, startTimeUtc, endTimeUtc, intervalUnit, repeatInterval)
        {
        }

        /// <summary>
        /// Create a <see cref="DateIntervalTrigger" /> that will occur at the given time,
        /// and repeat at the the given interval until the given end time.
        /// </summary>
        /// <param name="name">Name for the trigger instance.</param>
        /// <param name="group">Group for the trigger instance.</param>
        /// <param name="startTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to fire.</param>
        /// <param name="endTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to quit repeat firing.</param>
        /// <param name="intervalUnit">The repeat interval unit (minutes, days, months, etc).</param>
        /// <param name="repeatInterval">The number of milliseconds to pause between the repeat firing.</param>
        public DateIntervalTrigger(string name, string group, DateTime startTimeUtc,
                                   DateTime? endTimeUtc, IntervalUnit intervalUnit, int repeatInterval)
            : base(name, group)
        {
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            RepeatIntervalUnit = (intervalUnit);
            RepeatInterval = (repeatInterval);
        }

        /// <summary>
        /// Create a <see cref="DateIntervalTrigger" /> that will occur at the given time,
        /// and repeat at the the given interval until the given end time.
        /// </summary>
        /// <param name="name">Name for the trigger instance.</param>
        /// <param name="group">Group for the trigger instance.</param>
        /// <param name="jobName">Name of the associated job.</param>
        /// <param name="jobGroup">Group of the associated job.</param>
        /// <param name="startTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to fire.</param>
        /// <param name="endTimeUtc">A <code>Date</code> set to the time for the <code>Trigger</code> to quit repeat firing.</param>
        /// <param name="intervalUnit">The repeat interval unit (minutes, days, months, etc).</param>
        /// <param name="repeatInterval">The number of milliseconds to pause between the repeat firing.</param>
        public DateIntervalTrigger(string name, string group, string jobName,
                                   string jobGroup, DateTime startTimeUtc, DateTime? endTimeUtc,
                                   IntervalUnit intervalUnit, int repeatInterval)
            : base(name, group, jobName, jobGroup)
        {
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            RepeatIntervalUnit = intervalUnit;
            RepeatInterval = repeatInterval;
        }

        /// <summary>
        /// Get the time at which the <see cref="DateIntervalTrigger" /> should occur.
        /// </summary>
        public override DateTime StartTimeUtc
        {
            get
            {
                if (startTime == DateTime.MinValue)
                {
                    startTime = SystemTime.UtcNow();
                }
                return startTime;
            }
            set
            {
                if (value == DateTime.MinValue)
                {
                    throw new ArgumentException("Start time cannot be DateTime.MinValue");
                }

                DateTime? eTime = EndTimeUtc;
                if (eTime != null && eTime < value)
                {
                    throw new ArgumentException("End time cannot be before start time");
                }

                startTime = value;
            }
        }

        /// <summary>
        /// Tells whether this Trigger instance can handle events
        /// in millisecond precision.
        /// </summary>
        public override bool HasMillisecondPrecision
        {
            get { return true; }
        }

        /// <summary>
        /// Get the time at which the <see cref="DateIntervalTrigger" /> should quit
        /// repeating.
        /// </summary>
        public override DateTime? EndTimeUtc
        {
            get { return endTime; }
            set
            {
                DateTime sTime = StartTimeUtc;
                if (value != null && sTime > value)
                {
                    throw new ArgumentException("End time cannot be before start time");
                }

                endTime = value;
            }
        }

        /// <summary>
        /// Get or set the interval unit - the time unit on with the interval applies.
        /// </summary>
        public IntervalUnit RepeatIntervalUnit
        {
            get { return repeatIntervalUnit; }
            set { this.repeatIntervalUnit = value; }
        }


        /// <summary>
        /// Get the the time interval that will be added to the <see cref="DateIntervalTrigger" />'s
        /// fire time (in the set repeat interval unit) in order to calculate the time of the 
        /// next trigger repeat.
        /// </summary>
        public int RepeatInterval
        {
            get { return repeatInterval; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Repeat interval must be >= 1");
                }

                repeatInterval = value;
            }
        }

        /// <summary>
        /// Get the number of times the <code>DateIntervalTrigger</code> has already fired.
        /// </summary>
        public int TimesTriggered
        {
            get { return timesTriggered; }
            set { this.timesTriggered = value; }
        }


        /// <summary>
        /// Validates the misfire instruction.
        /// </summary>
        /// <param name="misfireInstruction">The misfire instruction.</param>
        /// <returns></returns>
        protected override bool ValidateMisfireInstruction(int misfireInstruction)
        {
            if (misfireInstruction < Quartz.MisfireInstruction.SmartPolicy)
            {
                return false;
            }

            if (misfireInstruction > Quartz.MisfireInstruction.DateIntervalTrigger.DoNothing)
            {
                return false;
            }

            return true;
        }

        /// <summary> 
        /// Updates the <see cref="DateIntervalTrigger" />'s state based on the
        /// MISFIRE_INSTRUCTION_XXX that was selected when the <code>DateIntervalTrigger</code>
        /// was created.
        /// </summary>
        /// <remarks>
        ///  If the misfire instruction is set to MISFIRE_INSTRUCTION_SMART_POLICY,
        /// then the following scheme will be used:
        /// <ul>
        /// <li>The instruction will be interpreted as <code>MISFIRE_INSTRUCTION_FIRE_ONCE_NOW</code></li>
        /// </ul>
        /// </remarks>
        public override void UpdateAfterMisfire(ICalendar cal)
        {
            int instr = MisfireInstruction;

            if (instr == Quartz.MisfireInstruction.SmartPolicy)
            {
                instr = Quartz.MisfireInstruction.DateIntervalTrigger.FireOnceNow;
            }

            if (instr == Quartz.MisfireInstruction.DateIntervalTrigger.DoNothing)
            {
                DateTime? newFireTime = GetFireTimeAfter(SystemTime.UtcNow());
                while (newFireTime != null && cal != null && !cal.IsTimeIncluded(newFireTime.Value))
                {
                    newFireTime = GetFireTimeAfter(newFireTime);
                }
                NextFireTimeUtc = newFireTime;
            }
            else if (instr == Quartz.MisfireInstruction.DateIntervalTrigger.FireOnceNow)
            {
                // fire once now...
                NextFireTimeUtc = SystemTime.UtcNow();
                // the new fire time afterward will magically preserve the original  
                // time of day for firing for day/week/month interval triggers, 
                // because of the way getFireTimeAfter() works - in its always restarting
                // computation from the start time.
            }
        }

        /// <summary>
        /// This method should not be used by the Quartz client.
        /// <p>
        /// Called when the <see cref="IScheduler" /> has decided to 'fire'
        /// the trigger (Execute the associated <see cref="IJob" />), in order to
        /// give the <see cref="Trigger" /> a chance to update itself for its next
        /// triggering (if any).
        /// </p>
        /// </summary>
        /// <seealso cref="JobExecutionException" />
        public override void Triggered(ICalendar calendar)
        {
            timesTriggered++;
            previousFireTime = nextFireTime;
            nextFireTime = GetFireTimeAfter(nextFireTime);

            while (nextFireTime != null && calendar != null
                   && !calendar.IsTimeIncluded(nextFireTime.Value))
            {
                nextFireTime = GetFireTimeAfter(nextFireTime);

                if (nextFireTime == null)
                    break;

                //avoid infinite loop
                if (nextFireTime.Value.Year > YearToGiveupSchedulingAt)
                {
                    nextFireTime = null;
                }
            }
        }

        /// <summary> 
        /// This method should not be used by the Quartz client.
        /// <p>
        /// The implementation should update the <see cref="Trigger" />'s state
        /// based on the given new version of the associated <see cref="ICalendar" />
        /// (the state should be updated so that it's next fire time is appropriate
        /// given the Calendar's new settings). 
        /// </p>
        /// </summary>
        /// <param name="calendar"> </param>
        /// <param name="misfireThreshold"></param>
        public override void UpdateWithNewCalendar(ICalendar calendar, TimeSpan misfireThreshold)
        {
            nextFireTime = GetFireTimeAfter(previousFireTime);

            if (nextFireTime == null || calendar == null)
            {
                return;
            }

            DateTime now = SystemTime.UtcNow();
            while (nextFireTime != null && !calendar.IsTimeIncluded(nextFireTime.Value))
            {
                nextFireTime = GetFireTimeAfter(nextFireTime);

                if (nextFireTime == null)
                    break;

                //avoid infinite loop
                if (nextFireTime.Value.Year > YearToGiveupSchedulingAt)
                {
                    nextFireTime = null;
                }

                if (nextFireTime != null && nextFireTime < now)
                {
                    TimeSpan diff = now - nextFireTime.Value;
                    if (diff >= misfireThreshold)
                    {
                        nextFireTime = GetFireTimeAfter(nextFireTime);
                    }
                }
            }
        }

        /// <summary>
        /// This method should not be used by the Quartz client.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Called by the scheduler at the time a <see cref="Trigger" /> is first
        /// added to the scheduler, in order to have the <see cref="Trigger" />
        /// compute its first fire time, based on any associated calendar.
        /// </p>
        /// 
        /// <p>
        /// After this method has been called, <see cref="Trigger.GetNextFireTimeUtc" />
        /// should return a valid answer.
        /// </p>
        /// </remarks>
        /// <returns> 
        /// The first time at which the <see cref="Trigger" /> will be fired
        /// by the scheduler, which is also the same value <see cref="Trigger.GetNextFireTimeUtc" />
        /// will return (until after the first firing of the <see cref="Trigger" />).
        /// </returns>        
        public override DateTime? ComputeFirstFireTimeUtc(ICalendar calendar)
        {
            nextFireTime = StartTimeUtc;

            while (nextFireTime != null && calendar != null
                   && !calendar.IsTimeIncluded(nextFireTime.Value))
            {
                nextFireTime = GetFireTimeAfter(nextFireTime);

                if (nextFireTime == null)
                    break;

                //avoid infinite loop
                if (nextFireTime.Value.Year > YearToGiveupSchedulingAt)
                {
                    return null;
                }
            }

            return nextFireTime;
        }

        /// <summary>
        /// This method should not be used by the Quartz client.
        /// </summary>
        /// <remarks>
        /// Called after the <see cref="IScheduler" /> has executed the
        /// <see cref="JobDetail" /> associated with the <see cref="Trigger" />
        /// in order to get the final instruction code from the trigger.
        /// </remarks>
        /// <param name="context">
        /// is the <see cref="JobExecutionContext" /> that was used by the
        /// <see cref="IJob" />'s<see cref="IJob.Execute" /> method.</param>
        /// <param name="result">is the <see cref="JobExecutionException" /> thrown by the
        /// <see cref="IJob" />, if any (may be null).
        /// </param>
        /// <returns>
        /// One of the <see cref="SchedulerInstruction"/> members.
        /// </returns>
        /// <seealso cref="SchedulerInstruction.NoInstruction" />
        /// <seealso cref="SchedulerInstruction.ReExecuteJob" />
        /// <seealso cref="SchedulerInstruction.DeleteTrigger" />
        /// <seealso cref="SchedulerInstruction.SetTriggerComplete" />
        /// <seealso cref="Trigger.Triggered" />
        public override SchedulerInstruction ExecutionComplete(JobExecutionContext context,
                                                               JobExecutionException result)
        {
            if (result != null && result.RefireImmediately)
            {
                return SchedulerInstruction.ReExecuteJob;
            }

            if (result != null && result.UnscheduleFiringTrigger)
            {
                return SchedulerInstruction.SetTriggerComplete;
            }

            if (result != null && result.UnscheduleAllTriggers)
            {
                return SchedulerInstruction.SetAllJobTriggersComplete;
            }

            if (!GetMayFireAgain())
            {
                return SchedulerInstruction.DeleteTrigger;
            }

            return SchedulerInstruction.NoInstruction;
        }

        /// <summary>
        /// Returns the next time at which the <see cref="Trigger" /> is scheduled to fire. If
        /// the trigger will not fire again, <see langword="null" /> will be returned.  Note that
        /// the time returned can possibly be in the past, if the time that was computed
        /// for the trigger to next fire has already arrived, but the scheduler has not yet
        /// been able to fire the trigger (which would likely be due to lack of resources
        /// e.g. threads).
        /// </summary>
        ///<remarks>
        /// The value returned is not guaranteed to be valid until after the <see cref="Trigger" />
        /// has been added to the scheduler.
        /// </remarks>
        /// <seealso cref="TriggerUtils.ComputeFireTimesBetween(Trigger, ICalendar , DateTime, DateTime)" />
        /// <returns></returns>
        public override DateTime? GetNextFireTimeUtc()
        {
            return nextFireTime;
        }

        /// <summary>
        /// Returns the previous time at which the <see cref="DateIntervalTrigger" /> fired.
        /// If the trigger has not yet fired, <see langword="null" /> will be returned.
        /// </summary>
        public override DateTime? GetPreviousFireTimeUtc()
        {
            return previousFireTime;
        }

        /**
     * <p>
     * Set the next time at which the <code>DateIntervalTrigger</code> should fire.
     * </p>
     * 
     * <p>
     * <b>This method should not be invoked by client code.</b>
     * </p>
     */

        public DateTime? NextFireTimeUtc
        {
            set { this.nextFireTime = value; }
        }

        /**
     * <p>
     * Set the previous time at which the <code>DateIntervalTrigger</code> fired.
     * </p>
     * 
     * <p>
     * <b>This method should not be invoked by client code.</b>
     * </p>
     */

        public DateTime PreviousFireTimeUtc
        {
            set { this.previousFireTime = value; }
        }

        /// <summary>
        /// Returns the next time at which the <see cref="DateIntervalTrigger" /> will fire,
        /// after the given time. If the trigger will not fire after the given time,
        /// <see langword="null" /> will be returned.
        /// </summary>
        public override DateTime? GetFireTimeAfter(DateTime? afterTime)
        {
            return GetFireTimeAfter(afterTime, false);
        }

        protected DateTime? GetFireTimeAfter(DateTime? afterTime, bool ignoreEndTime)
        {
            if (complete)
            {
                return null;
            }

            // increment afterTme by a second, so that we are 
            // comparing against a time after it!
            if (afterTime == null)
            {
                afterTime = SystemTime.UtcNow().AddSeconds(1);
            }
            else
            {
                afterTime = afterTime.Value.AddSeconds(1);
            }

            DateTime startMillis = StartTimeUtc;
            DateTime afterMillis = afterTime.Value;
            DateTime endMillis = (EndTimeUtc == null) ? DateTime.MaxValue : EndTimeUtc.Value;

            if (!ignoreEndTime && (endMillis <= afterMillis))
            {
                return null;
            }

            if (afterMillis < startMillis)
            {
                return startMillis;
            }

            long secondsAfterStart = (long)(afterMillis - startMillis).TotalSeconds;

            DateTime? time = null;
            long repeatLong = RepeatInterval;
            
            DateTime? aTime = afterTime;
            
            DateTime sTime = StartTimeUtc;

            if (RepeatIntervalUnit == IntervalUnit.Second)
            {
                long jumpCount = secondsAfterStart / repeatLong;
                if (secondsAfterStart % repeatLong != 0)
                {
                    jumpCount++;
                }
                time = sTime.AddSeconds(RepeatInterval * (int)jumpCount);
            }
            else if (RepeatIntervalUnit == IntervalUnit.Minute)
            {
                long jumpCount = secondsAfterStart / (repeatLong * 60L);
                if (secondsAfterStart % (repeatLong * 60L) != 0)
                {
                    jumpCount++;
                }
                time = sTime.AddMinutes(RepeatInterval * (int)jumpCount);
            }
            else if (RepeatIntervalUnit == IntervalUnit.Hour)
            {
                long jumpCount = secondsAfterStart / (repeatLong * 60L * 60L);
                if (secondsAfterStart % (repeatLong * 60L * 60L) != 0)
                {
                    jumpCount++;
                }
                time = sTime.AddHours(RepeatInterval * (int)jumpCount);
            }
            else if (RepeatIntervalUnit == IntervalUnit.Day)
            {
                // Because intervals greater than an hour have an non-fixed number 
                // of seconds in them (due to daylight savings, variation number of 
                // days in each month, leap year, etc. ) we can't jump forward an
                // exact number of seconds to calculate the fire time as we can
                // with the second, minute and hour intervals.   But, rather
                // than slowly crawling our way there by iteratively adding the 
                // increment to the start time until we reach the "after time",
                // we can first make a big leap most of the way there...

                long jumpCount = secondsAfterStart / (repeatLong * 24L * 60L * 60L);
                // if we need to make a big jump, jump most of the way there, 
                // but not all the way because in some cases we may over-shoot or under-shoot
                if (jumpCount > 20)
                {
                    if (jumpCount < 50)
                    {
                        jumpCount = (long)(jumpCount * 0.80);
                    }
                    else if (jumpCount < 500)
                    {
                        jumpCount = (long)(jumpCount * 0.90);
                    }
                    else
                    {
                        jumpCount = (long)(jumpCount * 0.95);
                    }
                    sTime = sTime.AddDays(RepeatInterval * jumpCount);
                }

                // now baby-step the rest of the way there...
                while (sTime < afterTime && sTime.Year < YearToGiveupSchedulingAt)
                {
                    sTime= sTime.AddDays(RepeatInterval);
                }
                time = sTime;
            }
            else if (RepeatIntervalUnit == IntervalUnit.Week)
            {
                // Because intervals greater than an hour have an non-fixed number 
                // of seconds in them (due to daylight savings, variation number of 
                // days in each month, leap year, etc. ) we can't jump forward an
                // exact number of seconds to calculate the fire time as we can
                // with the second, minute and hour intervals.   But, rather
                // than slowly crawling our way there by iteratively adding the 
                // increment to the start time until we reach the "after time",
                // we can first make a big leap most of the way there...

                long jumpCount = secondsAfterStart / (repeatLong * 7L * 24L * 60L * 60L);
                // if we need to make a big jump, jump most of the way there, 
                // but not all the way because in some cases we may over-shoot or under-shoot
                if (jumpCount > 20)
                {
                    if (jumpCount < 50)
                    {
                        jumpCount = (long)(jumpCount * 0.80);
                    }
                    else if (jumpCount < 500)
                    {
                        jumpCount = (long)(jumpCount * 0.90);
                    }
                    else
                    {
                        jumpCount = (long)(jumpCount * 0.95);
                    }
                    sTime = sTime.AddDays((int)(RepeatInterval * jumpCount * 7));
                }

                while (sTime < afterTime && sTime.Year < YearToGiveupSchedulingAt)
                {
                    sTime = sTime.AddDays(RepeatInterval * 7);
                }
                time = sTime;
            }
            else if (RepeatIntervalUnit == IntervalUnit.Month)
            {
                // because of the large variation in size of months, and 
                // because months are already large blocks of time, we will
                // just advance via brute-force iteration.
                while (sTime < afterTime && sTime.Year < YearToGiveupSchedulingAt)
                {
                    sTime = sTime.AddMonths(RepeatInterval);
                }
                time = sTime;
            }
            else if (RepeatIntervalUnit == IntervalUnit.Year)
            {
                while (sTime < afterTime && sTime.Year < YearToGiveupSchedulingAt)
                {
                    sTime = sTime.AddYears(RepeatInterval);
                }
                time = sTime;
            }

            if (!ignoreEndTime && endMillis <= time)
            {
                return null;
            }

            return time;
        }

        /// <summary>
        /// Returns the final time at which the <code>DateIntervalTrigger</code> will
        /// fire, if there is no end time set, null will be returned.
        /// </summary>
        /// <value></value>
        /// <remarks>Note that the return time may be in the past.</remarks>
        public override DateTime? FinalFireTimeUtc
        {
            get
            {
                if (complete || EndTimeUtc == null)
                {
                    return null;
                }

                // back up a second from end time
                DateTime? fTime = EndTimeUtc.Value.AddSeconds(-1);
                // find the next fire time after that
                fTime = GetFireTimeAfter(fTime, true);

                // the the trigger fires at the end time, that's it!
                if (fTime == EndTimeUtc)
                {
                    return fTime;
                }

                // otherwise we have to back up one interval from the fire time after the end time

                DateTime lTime = fTime.Value;

                if (RepeatIntervalUnit == IntervalUnit.Second)
                {
                    lTime = lTime.AddSeconds(-1 * RepeatInterval);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Minute)
                {
                    lTime = lTime.AddMinutes(-1 * RepeatInterval);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Hour)
                {
                    lTime = lTime.AddHours(-1 * RepeatInterval);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Day)
                {
                    lTime = lTime.AddDays(-1 * RepeatInterval);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Week)
                {
                    lTime = lTime.AddDays(-1 * RepeatInterval * 7);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Month)
                {
                    lTime = lTime.AddMonths(-1 * RepeatInterval);
                }
                else if (RepeatIntervalUnit == IntervalUnit.Year)
                {
                    lTime = lTime.AddYears( -1 * RepeatInterval);
                }

                return lTime;
            }
        }

        /// <summary>
        /// Determines whether or not the <code>DateIntervalTrigger</code> will occur
        /// again.
        /// </summary>
        /// <returns></returns>
        public override bool GetMayFireAgain()
        {
            return (GetNextFireTimeUtc() != null);
        }

        /// <summary>
        /// Validates whether the properties of the <see cref="JobDetail" /> are
        /// valid for submission into a <see cref="IScheduler" />.
        /// </summary>
        public override void Validate()
        {
            base.Validate();

            if (repeatInterval < 1)
            {
                throw new SchedulerException("Repeat Interval cannot be zero.", SchedulerException.ErrorClientError);
            }
        }
    }
}