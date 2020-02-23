using Cachet_Monitor.Models;
using Cachet_Monitor.Models.Config;
using Monitor = Cachet_Monitor.Models.Config.Monitor;
using CachNet;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using CachNet.Entities;
using System.Net.NetworkInformation;

namespace Cachet_Monitor
{
    internal class Program
    {
        private static Configuration Configuration;
        private static CachetClient Cachet;

        private static void Main()
        {
            {
                var config = Path.Combine(Environment.CurrentDirectory, "settings.json");

                if (File.Exists(config))
                {
                    Configuration = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(config));
                }
                else
                {
                    File.WriteAllText(config, JsonConvert.SerializeObject(Configuration.Default, Formatting.Indented));

                    Console.WriteLine($"Written a new instance of the configuration to: {config}");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
            }

            Cachet = new CachetClient(Configuration.ApiBase.OriginalString, Configuration.ApiKey);

            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            foreach (var monitor in Configuration.Monitors)
            {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        await DoMonitorCheck(Cachet, monitor).ConfigureAwait(false);

                        await Task.Delay(monitor.Interval * 1000);
                    }
                }).Start();
            }

            await Task.Delay(-1).ConfigureAwait(false);
        }

        private static async Task DoMonitorCheck(CachetClient Cachet, Monitor monitor)
        {
            ComponentStatus componentStatus = 0;
            switch (monitor.Type)
            {
                case MonitorType.PORT:
                    {
                        using (TcpClient tcpClient = new TcpClient())
                        {
                            try
                            {
                                tcpClient.Connect(monitor.Target, monitor.Settings.Port);
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.Operational,
                                });
                            }
                            catch (Exception)
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.MajorOutage,
                                });
                            }
                        }
                    }
                    break;

                case MonitorType.ICMP:
                    {
                        Ping ping = new Ping();
                        PingOptions options = new PingOptions
                        {
                            DontFragment = true,
                            Ttl = monitor.Settings.TTL
                        };

                        PingReply reply = ping.Send(monitor.Target, monitor.Timeout, null, options);

                        await Cachet.AddMetricPointAsync(monitor.MetricId, new PostMetricPoint
                        {
                            Value = (int)reply.RoundtripTime
                        });

                        if (reply.Status == IPStatus.Success)
                        {
                            await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                            {
                                Status = ComponentStatus.Operational,
                            });
                        }

                        if (reply.RoundtripTime >= monitor.Timeout)
                        {
                            await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                            {
                                Status = ComponentStatus.PerformanceIssues,
                            });
                        }

                        if (reply.Status != IPStatus.Success)
                        {
                            if (reply.Status == IPStatus.TimedOut)
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.PartialOutage,
                                });
                            }
                            else
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.MajorOutage,
                                });
                            }
                        }
                    }
                    break;

                case MonitorType.HTTP:
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(monitor.Target);
                        request.Timeout = monitor.Timeout * 1000;

                        Stopwatch timer = new Stopwatch();

                        timer.Start();
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            timer.Stop();

                            if (response.StatusCode == (HttpStatusCode)monitor.Settings.ExpectedStatusCode)
                            {
                                var x = await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.Operational,
                                });
                            }
                            else
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.PartialOutage,
                                });
                            }

                            response.Close();
                        }
                        catch (WebException ex)
                        {
                            timer.Stop();

                            if (ex.Status == WebExceptionStatus.ProtocolError)
                            {
                                if (ex.Response is HttpWebResponse response)
                                {
                                    if (response.StatusCode == (HttpStatusCode)monitor.Settings.ExpectedStatusCode)
                                    {
                                        await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                        {
                                            Status = ComponentStatus.Operational,
                                        });
                                    }
                                }
                            }
                            else
                            {
                                if (ex.Status == WebExceptionStatus.Timeout)
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.PerformanceIssues,
                                    });
                                }
                                else
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.MajorOutage,
                                    });
                                }
                            }
                        }

                        await Cachet.AddMetricPointAsync(monitor.MetricId, new PostMetricPoint
                        {
                            Value = (int)timer.ElapsedMilliseconds
                        });
                    }
                    break;
            }

            Console.WriteLine($"Ran check on \"{monitor.Target}\" Status: {componentStatus}");
        }
    }
}