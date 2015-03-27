﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Jarvis.ConfigurationService.Client.Support;

namespace Jarvis.ConfigurationService.Tests.Support
{
    public class TestWebClient
    {
        private WebClient _client;

        public TestWebClient()
        {
            _client = new WebClient();
        }

        public String DownloadString(String uri)
        {
            try
            {
                return _client.DownloadString(uri);
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    var responseStream = ex.Response.GetResponseStream();

                    if (responseStream != null)
                    {
                        using (var reader = new StreamReader(responseStream))
                        {
                            var responseText = reader.ReadToEnd();
                            Console.WriteLine("DownloadError:" + responseText);
                        }
                    }
                }
                throw;
            }
        }

    }
}