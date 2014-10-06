﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using Jarvis.ConfigurationService.Host.Model;
using Jarvis.ConfigurationService.Host.Support;
using Microsoft.SqlServer.Server;
using log4net;

namespace Jarvis.ConfigurationService.Host.Controllers
{
    public class ConfigController : ApiController
    {
        private static ILog _logger = LogManager.GetLogger(typeof(ConfigController));

        [HttpGet]
        [Route("")]
        public ServerStatusModel Status()
        {
            string baseDirectory = FileSystem.Instance.GetBaseDirectory();
            var applicationsDir = FileSystem.Instance
                .GetDirectories(baseDirectory)
                .Select(Path.GetFileName);
            var redirectedApps = FileSystem.Instance
                .GetFiles(baseDirectory, "*.redirect")
                .Select(f => Path.GetFileNameWithoutExtension(f));

            return new ServerStatusModel
            {
                BaseFolder = baseDirectory,
                Applications = applicationsDir.Union(redirectedApps).ToArray(),
                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
            };
        }

        [HttpGet]
        [Route("{appName}")]
        public HttpResponseMessage GetConfiguration(String appName)
        {
            var baseDirectory = FileSystem.Instance.GetBaseDirectory();
            var appFolder = Path.Combine(baseDirectory, appName, "Default");
            
            if (!FileSystem.Instance.DirectoryExists(appFolder, false))
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "App not found");

            string[] modules = FileSystem.Instance
                .GetFiles(appFolder, "*.config")
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray();

            return Request.CreateResponse(HttpStatusCode.OK, modules);
        }

        [HttpGet]
        [Route("{appName}/{moduleName}/config.json/{hostName=''}")]
        [Route("{appName}/{moduleName}.config/{hostName=''}")]
        public Object GetConfiguration(String appName, String moduleName, String hostName = "")
        {
            var baseDirectory = FileSystem.Instance.GetBaseDirectory();
            return ConfigFileLocator.GetConfig(baseDirectory, appName, moduleName, hostName);
        }

       
    }
}
