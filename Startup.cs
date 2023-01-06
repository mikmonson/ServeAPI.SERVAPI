using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;
using AdminPortal.Data;
using AdminPortal.Models;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SERVAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            FilePath.connection_string= configuration.GetValue<string>("DBConnection");
            FilePath.cert_path = configuration.GetValue<string>("cert_path");
            FilePath.device_path = configuration.GetValue<string>("device_path");
            FilePath.template_path = configuration.GetValue<string>("template_path");
            FilePath.path_separator = configuration.GetValue<string>("path_separator");
            FilePath.file_path = configuration.GetValue<string>("file_path");
            FilePath.certfile = configuration.GetValue<string>("certfile");
            FilePath.certkey = configuration.GetValue<string>("certkey");
            Debug.WriteLine("Settings have been loaded");
        }
        public IConfiguration Configuration { get; }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
   .AddCertificate(options =>
        {
           
       options.AllowedCertificateTypes = CertificateTypes.All;
       options.Events = new CertificateAuthenticationEvents
       {

           OnCertificateValidated = context =>
           {
               Boolean auth = false;
               System.Diagnostics.Debug.WriteLine("Analyzing client cert -> comparing to request query parameters");
               try
               {
                   var certdata = context.ClientCertificate.Subject;
                   string pubkey = Convert.ToBase64String(context.ClientCertificate.GetPublicKey());
                   string thumbprint = Convert.ToString(context.ClientCertificate.Thumbprint);
                   string urisn = Convert.ToString(context.Request.QueryString).ToLower();
                   string cn = "";
                   if ((certdata != null) && (urisn != null))
                   {
                       string s = Convert.ToString(certdata);
                       cn = "sn=" + s.Substring(3, s.IndexOf(',')-3);
                       System.Diagnostics.Debug.WriteLine("full cert subject: " + s);
                       System.Diagnostics.Debug.WriteLine("public key: " + pubkey);
                       System.Diagnostics.Debug.WriteLine("thumbprint: " + thumbprint);
                       System.Diagnostics.Debug.WriteLine("query: " + urisn);
                       System.Diagnostics.Debug.WriteLine("extracted serial number: " + cn.ToLower());
                       System.Diagnostics.Debug.WriteLine("query: " + urisn);

                       TrustedList tlist = new TrustedList(); //Read text file with client trusted certs
                       if (tlist.IsTrusted(thumbprint))
                       {
                           System.Diagnostics.Debug.WriteLine("Client certificate is trusted");
                           //Comparing cn in cert with sn from query
                           if (urisn.IndexOf(cn.ToLower()) > 0)
                           {
                               auth = true;
                               System.Diagnostics.Debug.WriteLine("Client certificate CN matched SN in the query stirng");
                           } else
                           {
                               System.Diagnostics.Debug.WriteLine("Client certificate CN didn't match SN in the query stirng");
                           }
                       }
                       else
                       {
                           System.Diagnostics.Debug.WriteLine("Client certificate is untrusted");
                       }
                      
                   }
                   if (auth==true)
                   {
                       context.Success();
                   }
                   else
                   {
                       context.Fail("invalid cert");
                   }
               }
               catch
               {
                   context.Fail("cert/uri validation failed");
               }
               return Task.CompletedTask;
           }
       };
   });


        }


      
    

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.

       
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
              if (env.IsDevelopment())
              {
                  app.UseDeveloperExceptionPage();
              } 
            //app.UseMyMiddleware();
            app.UseHttpsRedirection();
            //app.UseCertificateForwarding();
            app.UseAuthentication();
            //app.UseAuthorization();
            Router router = new Router();
           
            app.Run(async (context) =>
            {

                if (context.User.Identity.IsAuthenticated == false)
                {
                    System.Diagnostics.Debug.WriteLine("Client did not pass certificate authentication. Check client certificate and request query parameters");
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Certificate validation failed. Client certificate is invalid, untrusted or doesn't match parameters from URI.");
                }
                else
                {
                    try
                    {

                        var content = await router.RouteRequest(context.Request, context.Connection.RemoteIpAddress.ToString());
                        
                        context.Response.StatusCode = Convert.ToInt32(content.StatusCode); //Overwrite status code
                        await context.Response.WriteAsync(await content.Content.ReadAsStringAsync());
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("ERROR! Webserver failed while processing http request!");
                    }
                }
            });
        }

        
    }
}
