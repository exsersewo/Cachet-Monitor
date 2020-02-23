namespace Cachet_Monitor.Models.Config
{
    public class MonitorSettings
    {
        //Http
        public string Method = "GET";

        public int ExpectedStatusCode = 200;

        //TCP
        public int Port;

        public int TTL;
    }
}