using Cachet_Monitor.Models.Config;
using System;
using System.Collections.Generic;

namespace Cachet_Monitor.Models
{
    public class Configuration
    {
        public Uri ApiBase { get; set; }
        public string ApiKey { get; set; }
        public LogSeverity LogLevel { get; set; }
        public bool LogToFile { get; set; }

        public List<Monitor> Monitors { get; set; }

        public static Configuration Default = new Configuration
        {
            ApiBase = new Uri("https://domain.example.com"),
            ApiKey = "AAABBBCCC11223344556",
            LogLevel = LogSeverity.Info,
            LogToFile = true,
            Monitors = new List<Monitor>
            {
                new Monitor
                {
                    Name = "Website",
                    Target = "https://example.com",
                    ComponentId = 1,
                    MetricId = 1,
                    Interval = 60,
                    Timeout = 5,
                    Type = MonitorType.HTTP,
                    Settings = new MonitorSettings
                    {
                        ExpectedStatusCode = 200,
                        Method = "GET"
                    }
                },
                new Monitor
                {
                    Name = "Database",
                    Target = "example.com",
                    ComponentId = 1,
                    MetricId = 1,
                    Interval = 60,
                    Timeout = 5,
                    Type = MonitorType.PORT,
                    Settings = new MonitorSettings
                    {
                        Port = 3306
                    }
                },
                new Monitor
                {
                    Name = "Server",
                    Target = "example.com",
                    ComponentId = 1,
                    MetricId = 1,
                    Interval = 60,
                    Timeout = 5,
                    Type = MonitorType.ICMP
                }
            }
        };
    }
}