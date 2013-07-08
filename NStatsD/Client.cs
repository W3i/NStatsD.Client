using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Sockets;
using System.Threading;

namespace NStatsD
{
    public class Client
    {
        private static Client _current;
        public static Client Current
        {
            get
            {
                if (_current == null)
                    _current = new Client();
                return _current;
            }
            set { _current = value; }
        }

        private StatsDConfigurationSection _config;
        public StatsDConfigurationSection Config
        {
            get
            {
                if (_config == null)
                    _config = (StatsDConfigurationSection)ConfigurationManager.GetSection("statsD");
                return _config;
            }
        }

        public void Timing(string stat, long time, double sampleRate = 1)
        {
            var data = new Dictionary<string, string> { { stat, string.Format("{0}|ms", time) } };

            SendConsideringSampleRate(data, sampleRate);
        }

        public void Increment(string stat, double sampleRate = 1)
        {
            UpdateStats(stat, 1, sampleRate);
        }

        public void Decrement(string stat, double sampleRate = 1)
        {
            UpdateStats(stat, -1, sampleRate);
        }

        public void Gauge(string stat, int value)
        {
            var data = new Dictionary<string, string> {{stat, string.Format("{0}|g", value)}};
            SendWithoutSampleRate(data);
        }

        public void UpdateStats(string stat, int delta = 1, double sampleRate = 1)
        {
            var dictionary = new Dictionary<string, string> {{stat, string.Format("{0}|c", delta)}};
            SendConsideringSampleRate(dictionary, sampleRate);
        }

        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

        private void SendConsideringSampleRate(Dictionary<string, string> data, double sampleRate = 1)
        {
            if (Config == null)
            {
              return;
            }

            Dictionary<string, string> sampledData;
            if (sampleRate < 1 && _random.Value.NextDouble() <= sampleRate)
            {
                sampledData = new Dictionary<string, string>();
                foreach (var stat in data.Keys)
                {
                    sampledData.Add(stat, string.Format("{0}|@{1}", data[stat], sampleRate));
                }
            }
            else if (sampleRate >= 1)
            {
                sampledData = data;
            }
            else
            {
                // Didn't meet the sample criteria; don't send anything to StatsD.
                return;
            }

            ConnectAndSendDataOverUdp(sampledData);
        }

        private void SendWithoutSampleRate(Dictionary<string, string> data)
        {
            if (Config == null)
            {
                return;
            }

            ConnectAndSendDataOverUdp(data);
        }

        private void ConnectAndSendDataOverUdp(Dictionary<string, string> sampledData)
        {
            var host = Config.Server.Host;
            var port = Config.Server.Port;
            using (var client = new UdpClient(host, port))
            {
                foreach (var stat in sampledData.Keys)
                {
                    var encoding = new System.Text.ASCIIEncoding();
                    var stringToSend = string.Format("{0}:{1}", stat, sampledData[stat]);
                    var sendData = encoding.GetBytes(stringToSend);
                    client.BeginSend(sendData, sendData.Length, Callback, null);
                }
            }
        }

        private static void Callback(IAsyncResult result)
        {
            // dont really want to do anything here since, would rather miss metrics than cause a site/app failure
        }
    }
}
