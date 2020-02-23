namespace Cachet_Monitor.Models.Config
{
    public class Monitor
    {
        //Meta
        public string Name;

        public string Target;

        //Cachet
        public int ComponentId;

        public int MetricId;

        //Timing
        public int Interval;

        public int Timeout;

        public MonitorType Type;

        public MonitorSettings Settings;
    }
}