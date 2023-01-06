using AdminPortal.Data;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SERVAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
 
            //CreateWebHostBuilder(args).Build().Run();
            CreateHostBuilder(args).Build().Run();
           
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();


        public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>()
            .UseUrls("https://0.0.0.0:5001")

            .ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxConcurrentConnections = 100;
                serverOptions.Limits.MaxConcurrentUpgradedConnections = 100;
                serverOptions.Limits.MaxRequestBodySize = 10 * 1024;
                serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
                //serverOptions.Listen(IPAddress.Loopback, 5000);
                //serverOptions.Listen(IPAddress.Loopback, 5001);

                serverOptions.ConfigureHttpsDefaults(opt =>
                {
                    opt.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    //System.Diagnostics.Debug.WriteLine("ssssssssssssssssssssssssssss:"+Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                    opt.ServerCertificate = new X509Certificate2(FilePath.cert_path + FilePath.certfile, FilePath.certkey);
                    opt.SslProtocols = SslProtocols.Tls12;
                    opt.ClientCertificateValidation = (cert, chain, policyErrors) =>
                    {
                        return true;
                    };
                });

                // Set properties and call methods on options
            });            
        });

    }
}
