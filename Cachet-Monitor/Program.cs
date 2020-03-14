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

            Log.Configure(Configuration);

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
                        try
                        {
                            await DoMonitorCheck(Cachet, monitor).ConfigureAwait(false);
                            Log.Verbose("MainAsync", $"Ran Monitor Check for {monitor.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("MainAsync", $"{monitor.Name} failed with {ex.Message}", ex);
                        }

                        Log.Verbose("MainAsync", $"Halting {monitor.Name} check for {monitor.Interval * 1000}ms");
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
                        using TcpClient tcpClient = new TcpClient();
                        try
                        {
                            tcpClient.Connect(monitor.Target, monitor.Settings.Port);
                            try
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.Operational,
                                });
                                Log.Verbose("PortMonitorCheck", "Sent to Cachet successfully");
                            }
                            catch (Exception ex)
                            {
                                Log.Error("PortMonitorCheck", ex.Message, ex);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("PortMonitorCheck", ex.Message, ex);

                            try
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.MajorOutage,
                                });
                                Log.Verbose("PortMonitorCheck", "Sent to Cachet successfully");
                            }
                            catch(Exception ex2)
                            {
                                Log.Error("PortMonitorCheck", ex2.Message, ex2);
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

                        try
                        {
                            await Cachet.AddMetricPointAsync(monitor.MetricId, new PostMetricPoint
                            {
                                Value = (int)reply.RoundtripTime
                            });
                            Log.Verbose("IPMonitorCheck", "Sent to Cachet successfully");
                        }
                        catch (Exception ex)
                        {
                            Log.Error("IPMonitorCheck", ex.Message, ex);
                        }

                        if (reply.Status == IPStatus.Success)
                        {
                            try
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.Operational,
                                });
                                Log.Verbose("IPMonitorCheck", "Sent to Cachet successfully");
                            }
                            catch (Exception ex)
                            {
                                Log.Error("IPMonitorCheck", ex.Message, ex);
                            }
                        }

                        if (reply.RoundtripTime >= monitor.Timeout)
                        {
                            try
                            {
                                await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                {
                                    Status = ComponentStatus.PerformanceIssues,
                                });
                                Log.Verbose("IPMonitorCheck", "Sent to Cachet successfully");
                            }
                            catch (Exception ex)
                            {
                                Log.Error("IPMonitorCheck", ex.Message, ex);
                            }
                        }

                        if (reply.Status != IPStatus.Success)
                        {
                            if (reply.Status == IPStatus.TimedOut)
                            {
                                try
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.PartialOutage,
                                    });
                                    Log.Verbose("IPMonitorCheck", "Sent to Cachet successfully");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("IPMonitorCheck", ex.Message, ex);
                                }
                            }
                            else
                            {
                                try
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.MajorOutage,
                                    });
                                    Log.Verbose("IPMonitorCheck", "Sent to Cachet successfully");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("IPMonitorCheck", ex.Message, ex);
                                }
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
                                try
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.Operational,
                                    });
                                    Log.Verbose("WebMonitorCheck", "Sent to Cachet successfully");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("WebMonitorCheck", ex.Message, ex);
                                }
                            }
                            else
                            {
                                try
                                {
                                    await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                    {
                                        Status = ComponentStatus.PartialOutage,
                                    });
                                    Log.Verbose("WebMonitorCheck", "Sent to Cachet successfully");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("WebMonitorCheck", ex.Message, ex);
                                }

                            }

                            response.Close();
                        }
                        catch (WebException ex)
                        {
                            timer.Stop();
                            Log.Warning("WebMonitorChcek", ex.Message, ex);

                            if (ex.Status == WebExceptionStatus.ProtocolError)
                            {
                                if (ex.Response is HttpWebResponse response)
                                {
                                    if (response.StatusCode == (HttpStatusCode)monitor.Settings.ExpectedStatusCode)
                                    {
                                        try
                                        {
                                            await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                            {
                                                Status = ComponentStatus.Operational,
                                            });
                                            Log.Verbose("WebMonitorCheck", "Sent to Cachet successfully");
                                        }
                                        catch (Exception ex2)
                                        {
                                            Log.Error("WebMonitorCheck", ex2.Message, ex2);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (ex.Status == WebExceptionStatus.Timeout)
                                {
                                    try
                                    {
                                        await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                        {
                                            Status = ComponentStatus.PerformanceIssues,
                                        });
                                        Log.Verbose("WebMonitorCheck", "Sent to Cachet successfully");
                                    }
                                    catch (Exception ex2)
                                    {
                                        Log.Error("WebMonitorCheck", ex2.Message, ex2);
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        await Cachet.UpdateComponentAsync(monitor.ComponentId, new PutComponent
                                        {
                                            Status = ComponentStatus.MajorOutage,
                                        });
                                        Log.Verbose("WebMonitorCheck", "Sent to Cachet successfully");
                                    }
                                    catch (Exception ex2)
                                    {
                                        Log.Error("WebMonitorCheck", ex2.Message, ex2);
                                    }
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

            Log.Verbose("DoMonitorCheck", $"Ran check on \"{monitor.Name}\" Status: {componentStatus}");
        }
    }
}