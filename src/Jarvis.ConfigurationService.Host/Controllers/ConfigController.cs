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

        private static String version;

        private static String informationalVersion;

        static ConfigController()
        {
            version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            var informationalAttribute = Attribute
                .GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyInformationalVersionAttribute))
                as AssemblyInformationalVersionAttribute;
            if (informationalAttribute != null)
                informationalVersion = informationalAttribute.InformationalVersion;
        }

        [HttpGet]
        [Route("status")]
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
                Version = version,
                InformationalVersion = informationalVersion,
            };
        }

        [HttpGet]
        [Route("{appName}/status")]
        public HttpResponseMessage GetConfiguration(String appName)
        {
            var baseDirectory = FileSystem.Instance.GetBaseDirectory();
            var appFolder = Path.Combine(baseDirectory, appName, "Default");
            var redirected = FileSystem.Instance.RedirectDirectory(appFolder);
            if (!FileSystem.Instance.DirectoryExists(redirected, false))
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "App not found");

            string[] modules = FileSystem.Instance
                .GetFiles(redirected, "*.config")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(p => !"base".Equals(p, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Request.CreateResponse(HttpStatusCode.OK, modules);
        }

        [HttpGet]
        [Route("{appName}/{moduleName}/config.json/{hostName=}")]
        [Route("{appName}/{moduleName}.config/{hostName=}")]
        public Object GetConfiguration(String appName, String moduleName, String hostName = "")
        {
            var baseDirectory = FileSystem.Instance.GetBaseDirectory();
            return ConfigFileLocator.GetConfig(baseDirectory, appName, moduleName, hostName);
        }

        [HttpGet]
        [Route("{appName}/resources/{moduleName}/{filename}/{hostName=}")]
        public HttpResponseMessage GetConfiguration(String appName, String moduleName, String fileName, String hostName = "")
        {
            var baseDirectory = FileSystem.Instance.GetBaseDirectory();
            var resourceContent = ConfigFileLocator.GetResourceFile(baseDirectory, appName, moduleName, hostName, fileName);
            return
                new HttpResponseMessage()
                {
                    Content = new StringContent(resourceContent, Encoding.UTF8, "text/html")
                };
        }
    }
}
