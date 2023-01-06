using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Constraints;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SERVAPI
{
    public class Edge
    {
        public class Client  //EDGE Client Description
        {
            public string sn = "";
            public string mac = "";
            public string model = "";
            public string hostname = "";
            public string location = "";
            public int customer_id;
            public string servapi_host;
            public string servapi_port;
            public string provisioned;
            public string ssh_enabled;
            public string ssh_command;
            public string client_update_freq; //shoule have crontab format like "0 0 0 0 0  python /root/client.py"
            public string program_update_freq; //shoule have crontab format like "0 0 0 0 0 ./root/program"
            public string client_version;
            public string program_dir;
            //More fields to be added
        }
        public class StandardRequest  //Standard HTTP request parameters from EDGE
        {
            public string sn = "";
            public string mac = "";
            public string model = "";
            public string hostname = "";
            public string location = "";
            public int customer_id = 0;
            public long uptime;  // Uptime in sec
            public string ip = "";
            public double memory; //Memory utilization, %
            public double cpu; //CPU load instant, %
            public double free_disk; //Free size in / MB
            public string time = "";
            public string id; //Используется для сопоставления реквестов отправки команды/апдейта с ответом клиента
            public string custom_field = ""; //used for reporting faults and future
            public string filename; // For program data upload
            public string filetype; // For program data upload
            public double network; //reserved
        }

        public static StandardRequest ExtractStandardRequest(String query)
        {
            try
            {
                var dict = Nancy.Helpers.HttpUtility.ParseQueryString(query);
                string json = JsonConvert.SerializeObject(dict.Cast<string>().ToDictionary(k => k, v => dict[v]));
                return JsonConvert.DeserializeObject<Edge.StandardRequest>(json);
            } catch
            {
                System.Diagnostics.Debug.WriteLine("Edge.ExtractStandardRequest-> Failed to process client uri " + query);
                return null;
            }
        }

        public class RequestSnapshot
        {

            public string method = "";
            public string uri = "";
            public string cookies = "";
            public string mtype = "";
            public string body = "";
            public string path = "";

            public RequestSnapshot(HttpRequest req)
            {
                //var dict = HttpUtility.ParseQueryString()
                if (req.Method != null) method = req.Method.ToString().ToUpper();
                if (req.QueryString != null) uri = Convert.ToString(req.QueryString).ToLower();
                if (req.Cookies != null) cookies = req.Cookies.ToString();
                if (req.ContentType != null) mtype = req.ContentType.ToString();
                if (req.Body != null) body = req.Body.ToString();
                if (req.Path != null) path = req.Path.ToString().ToLower();
            }
        }
        public class Response_Command
        {
            public string action = ""; //command (list of commands) update or standby (do nothing)
            public List<string> payload = new List<string>();
            public string id; //request id -> to figure out folder
        }

        public class Response_Update
        {
            public string action = ""; //command (list of commands) update or standby (do nothing)
            public string location = "";
            public string command = "";
            public List<long> files_ind = new List<long>();
            public List<string> files = new List<string>();
            public string id; //request id -> to figure out folder
        }

        public class Response_Standby
        {
            readonly public string action = "standby"; //command (list of commands) update or standby (do nothing)

        }
        public class Response_Settings
        {
            public string action = ""; //command > settings
            public string servapi_host;
            public string servapi_port;
            public string provisioned;
            public string ssh_enabled;
            public string ssh_command;
            public string client_update_freq; //shoule have crontab format like "0 0 0 0 0  python /root/client.py"
            public string program_update_freq; //shoule have crontab format like "0 0 0 0 0 ./root/program"
            public string client_version;
            public string program_dir;
            public string id; //request id -> to figure out folder
        }

    }
    
}
