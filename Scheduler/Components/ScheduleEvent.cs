using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduler.Components
{
    [AddINotifyPropertyChangedInterface]
    public class ScheduleEvent
    {
        /// <summary>
        /// Ratio of StartTime of the event to the Duration of the TimeLine
        /// </summary>
        public double StartTimeRatio { get; set; }
        /// <summary>
        /// Ratio of Duration of the event to the Duration of the TimeLine
        /// </summary>
        public double DurationRatio { get; set; }
        public int StartTime { get; set; }

        public int Duration { get; set; }

        /// <summary>
        /// 1 - Pending
        /// 2 - Jeopardy
        /// 3 - Completed
        /// </summary>
        public int Status { get; set; }
        public bool IsRendered { get; set; }
        public int RenderLayer { get; set; }
    }
}
