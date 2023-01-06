using AdminPortal.Data;
using AdminPortal.Models;
using Nancy.Json;
using Newtonsoft.Json;
using SERVAPI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SERVAPI
{
    public class Queue_Item
    {
        public string command;
        public long id;

        public Queue_Item(string _command, long _id)
        {
            command = _command;
            id = _id;
        }
    }
    public class Queue
    {
        public string client;
        public List<Queue_Item> items;

        public Queue(string _client)
        {
            client = _client;
            items = new List<Queue_Item>();
        }
    }
    public class DBQueueData
    {
        private Queue_Item _lastqueueitem = null;
        private string _client_sn;
        private int _customer_id;
        public string _lastcommand;
        public Edge.Response_Settings _lastloadedsettings = null;
        private protected ServapiDBContext dbc;
        public DBQueueData(string sn, int customer_id, ServapiDBContext _dbc)
        {
            dbc = _dbc;
            _client_sn = sn.ToLower();
            _customer_id = customer_id;
            _lastqueueitem = LoadData(_client_sn);
        }
        
        public Queue_Item LoadData(string sn)
        {
            _lastcommand = "";
            Queue_Item qi = null;
            try
            {
                ClientTask ct = dbc.ClientTasks.FirstOrDefault(s => s.sn == sn && s.customer_id == _customer_id && s.direction==1 && ((s.status=="pending") || (s.status == "failing")));
                if (ct != null)
                {
                    Debug.WriteLine("DBQueueData->LoadData: pending task for client "+sn+" - taskid: "+Convert.ToString(ct.id));
                    qi = new Queue_Item(ct.task_type, ct.id);
                    _lastcommand = ct.task_type;
                }
                else Debug.WriteLine("DBQueueData->LoadData: no pending tasks for client ",sn);
            }
            catch
            {
                Debug.WriteLine("DBQueueData->LoadData: Can't read tasks from DB for client ", sn);
            }

            return qi;
        }
  
        public bool UpdateQueue(long requestid, string buffer)
        {
            bool res = false;

            if (requestid == _lastqueueitem.id)
            {
                try
                {
                    ClientTask ct = dbc.ClientTasks.First(s => s.id==requestid && s.sn == _client_sn && s.customer_id == _customer_id && s.direction == 1);
                    if (ct != null)
                    {
                        ct.status = "completed";
                        if (buffer != null)
                        {
                            
                            DataItem di = new DataItem();
                            di.sn = _client_sn;
                            di.tasktype = 2;
                            di.timestamp = DateTime.Now;
                            di.file_name = "result.txt";
                            di.content_type = "text";
                            di.content = Encoding.ASCII.GetBytes(buffer);
                            di.task_id = requestid;
                            di.customer_id = _customer_id;
                            dbc.DataItems.Add(di);
                            dbc.SaveChanges();
                            ct.result_file = di.id;
                        }
                        //Writing logs and linking it with taskid
                        ClientLog cl = new ClientLog();
                        cl.message = "Task "+Convert.ToString(requestid)+" has been completed successfully.";
                        cl.title = "TASK COMPLETION";
                        cl.timestamp = DateTime.Now;
                        cl.customer_id = _customer_id;
                        cl.sn = _client_sn;
                        cl.clienttask_id = requestid;
                        dbc.ClientLogs.Add(cl);
                        dbc.SaveChanges();
                        ct.client_log_id = cl.id;
                        dbc.ClientTasks.Update(ct);
                        dbc.SaveChanges();
                        res = true;
                    }
                    
                } catch
                {
                    Debug.WriteLine("DBQueueData->UpdateQueue: Couldn't find specfied task - " + Convert.ToString(requestid));
                }
            }
            return res;
        }
        public void AddErrorToTask(string buffer)
        {
            try
            {
                ClientLog cl = new ClientLog();
                cl.message = buffer;
                cl.title = "TASK ERROR";
                cl.timestamp = DateTime.Now;
                cl.customer_id = _customer_id;
                cl.sn = _client_sn;
                cl.clienttask_id = _lastqueueitem.id;
                dbc.ClientLogs.Add(cl);
                dbc.SaveChanges();

                ClientTask ct = dbc.ClientTasks.First(s => s.id == _lastqueueitem.id && s.sn == _client_sn && s.customer_id == _customer_id);
                if (ct != null)
                {
                    ct.client_log_id = cl.id;
                    ct.status = "failing";
                    dbc.ClientTasks.Update(ct);
                    dbc.SaveChanges();
                }
                else Debug.WriteLine("DBQueueData.AddErrorToTask-> Failed to find related task - taskid:" + Convert.ToString(_lastqueueitem.id));
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("DBQueueData.AddErrorToTask-> Failed to add error log in DB");
            }
        }

        public MultipartContent GetFilesFromDB(Edge.Response_Update resp_edge)
        {
            System.Diagnostics.Debug.WriteLine("GET->ProcessEngeUpdate Method execution->forming multipart response body");
            MultipartContent multipartContent = new MultipartContent("x-mixed-replace", "filedata");
            byte[] data = null;
            try
            {
                for (int i = 0; i < resp_edge.files.Count; i++)
                {
                    DataItem di = dbc.DataItems.First(s => s.id== resp_edge.files_ind[i] && s.sn==_client_sn && s.customer_id==_customer_id);
                    if (di!=null)
                    {
                        data = Encoding.UTF8.GetBytes(Convert.ToBase64String(di.content)); //Convert to base64
                        HttpContent content1 = new ByteArrayContent(data);
                        content1.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        content1.Headers.ContentDisposition = new ContentDispositionHeaderValue(resp_edge.files[i]);
                        content1.Headers.ContentLength = data.Length;
                        multipartContent.Add(content1);

                    } else System.Diagnostics.Debug.WriteLine("DBQueueData.GetFilesFromDB-> Couldn't load file for upload!");
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("DBQueueData.GetFilesFromDB-> Couldn't load file for upload!");
                return null;
            }
            return multipartContent;
        }
        public Edge.Response_Command PrepareCommand()
        {
            Edge.Response_Command _result = null;
            try
            {
                ClientTask ct = dbc.ClientTasks.First(s => s.id == _lastqueueitem.id && s.sn == _client_sn && s.customer_id == _customer_id);
                if (ct != null)
                {
                    _result = new Edge.Response_Command();
                    _result.action = "command";
                    _result.id = Convert.ToString(_lastqueueitem.id); //add queue item id
                    if (ct.JSON_Command_commands == null) ct.JSON_Command_commands = "";
                    AdminPortal.Models.TaskCommands tc = JsonConvert.DeserializeObject<AdminPortal.Models.TaskCommands>(ct.JSON_Command_commands);
                    for (int i=0; i < tc._commands.Count; i++)
                    {
                        _result.payload.Add(tc._commands[i].command);
                    }
                }
            } catch
            {
                System.Diagnostics.Debug.WriteLine("DBQueueData.PrepareCommand-> Can't extract commands from DB task " + Convert.ToString(_lastqueueitem.id));
            }
            return _result;
        }

        public Edge.Response_Settings PrepareSettings()
        {
            Edge.Response_Settings _result = null;
            try
            {
                ClientTask ct = dbc.ClientTasks.First(s => s.id == _lastqueueitem.id && s.sn == _client_sn && s.customer_id == _customer_id);
                if (ct != null)
                {
                    _result = new Edge.Response_Settings();
                    if (ct.json_params == null) ct.json_params = "";
                    _result = JsonConvert.DeserializeObject<Edge.Response_Settings>(ct.json_params);
                    _result.action = "settings";
                    _result.id = Convert.ToString(_lastqueueitem.id); //add queue item id
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("DBQueueData.PrepareSettings-> Can't load settings task from DB. Task id: " + _lastqueueitem.id);
            }
            return _result;
        }
        public Edge.Response_Update PrepareUpdate()
        {
            Edge.Response_Update _result = null;
            try
            {
                ClientTask ct = dbc.ClientTasks.First(s => s.id == _lastqueueitem.id && s.sn == _client_sn && s.customer_id == _customer_id);
                if (ct != null)
                {
                    _result = new Edge.Response_Update();
                    _result.action = "update";
                    _result.id = Convert.ToString(_lastqueueitem.id); //add queue item id
                    if (ct.JSON_File_source_files==null) ct.JSON_File_source_files = "";
                    AdminPortal.Models.List.TaskFiles tf = JsonConvert.DeserializeObject<AdminPortal.Models.List.TaskFiles>(ct.JSON_File_source_files);
                    for (int i = 0; i < tf._files.Count; i++)
                    {
                        _result.files.Add(tf._files[i].file_name);
                        _result.files_ind.Add(tf._files[i].content_index);
                    }
                    _result.location = tf.location;
                    if (ct.JSON_Command_commands == null) ct.JSON_Command_commands = "";
                        if (ct.JSON_Command_commands =="") {
                        _result.command = "";
                    }
                        else
                    {
                        AdminPortal.Models.TaskCommands tc = JsonConvert.DeserializeObject<AdminPortal.Models.TaskCommands>(ct.JSON_Command_commands);
                        if (tc._commands.Count > 0) _result.command = tc._commands[0].command;
                    }
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("DBQueueData.PrepareUpdate-> Can't load files/commands from DB for task id: " + Convert.ToString(_lastqueueitem.id));
            }
            return _result;
        }
      
    }

    public class DBClientData
    {

        public Edge.Client this_client=null;

        private protected ServapiDBContext dbc;

        public DBClientData(string sn, ServapiDBContext _dbc)
        {
            dbc = _dbc;
            this_client = LoadClientData(sn);
        }
      
        public void UpdateJournal(string title, string buffer)
        {
            if (this_client != null)
            {
                try
                {
                    ClientLog cl = new ClientLog();
                    cl.message = buffer;
                    cl.title = title;
                    cl.timestamp = DateTime.Now;
                    cl.customer_id = this_client.customer_id;
                    cl.sn = this.this_client.sn;
                    cl.clienttask_id = 0;
                    dbc.ClientLogs.Add(cl);
                    dbc.SaveChanges();
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("DBClientData.UpdateJournal-> Failed to add log in DB for client " + this_client.sn);
                }
            } else
            {
                System.Diagnostics.Debug.WriteLine("DBClientData.UpdateJournal-> Can't update journal as client data is null!");
            }
        }
          
        public Edge.Client LoadClientData(string sn)
        {
            try
            {
                AdminPortal.Models.Client cl = dbc.Clients.First(s => s.sn == sn.ToLower());
                Edge.Client ec = new Edge.Client();                
                ec.sn = cl.sn;              
                ec.customer_id = cl.customer_id;
                ec.hostname = cl.hostname;
                ec.location = cl.location;
                ec.mac = cl.mac;
                ec.model = cl.model;
                ec.provisioned = cl.provisioned;
                ec.servapi_host = cl.servapi_host;
                ec.servapi_port = cl.servapi_port;
                ec.ssh_command = cl.ssh_command;
                ec.ssh_enabled = cl.ssh_enabled;
                ec.client_update_freq = cl.client_update_freq;
                ec.client_version = cl.client_version;
                ec.program_update_freq = cl.program_update_freq;
                ec.program_dir = cl.program_dir;
                return ec;
            } catch
            {
                System.Diagnostics.Debug.WriteLine("DBClientData.LoadClientData-> Failed to load client data from DB " + sn);
                return null;
            }
        }

        public long SaveUploadedFile(byte[] buffer, string filetype, string filename)
        {
            long res = 0;
            try
            {
                //New dataitem for the uploaded file
                long dataid = 0;
                ClientTask ct = null;
                DataItem di = new DataItem();
                di.sn = this_client.sn;
                di.tasktype = 2;
                di.timestamp = DateTime.Now;
                di.file_name = filename;
                di.content_type = filetype;
                di.content = buffer;
                di.customer_id = this_client.customer_id;
                dbc.DataItems.Add(di);

                //Writing logs and linking it with taskid
                long logid = 0;
                ClientLog cl = new ClientLog();
                cl.title = "UPLOAD TASK REPORT";
                cl.timestamp = DateTime.Now;
                cl.customer_id = this_client.customer_id;
                cl.sn = this_client.sn;
                //cl.clienttask_id = ct.id;
                dbc.ClientLogs.Add(cl);

                dbc.SaveChanges();
                dataid = di.id;
                logid = cl.id;

                ct = new ClientTask();
                ct.sn = this_client.sn;
                ct.customer_id = this_client.customer_id;
                ct.status = "pending";
                ct.timestamp = DateTime.Now;
                ct.task_type = "upload";
                ct.direction = 2;
                ct.client_log_id = 0; //---------
                AdminPortal.Models.List.TaskFiles tf = new AdminPortal.Models.List.TaskFiles("");
                tf._files.Add(new AdminPortal.Models.List.JSON_File(dataid, filetype, filename));
                ct.JSON_File_source_files = new JavaScriptSerializer().Serialize(tf);
                ct.client_log_id = logid;                
                dbc.ClientTasks.Add(ct);
                dbc.SaveChanges();               

                di.task_id = ct.id;
                dbc.DataItems.Update(di);
                cl.clienttask_id = ct.id;
                cl.message = "Upload job of " + filename + " has been completed. Task " + Convert.ToString(ct.id) + " is pending data processing.";
                dbc.ClientLogs.Update(cl);
                dbc.SaveChanges();

                res = ct.id;
            } catch
            {
                System.Diagnostics.Debug.WriteLine("DBClientData.SaveUploadedFile-> Couldn't save client file " + filename + " in DB and create task");
            }

            return res;
        }

        public void UpdateClientMetrics(Edge.StandardRequest _ro, string client_ip)
        {
            try
            {
                ClientMetric cm = new ClientMetric();
                cm.sn = this_client.sn;
                cm.customer_id = this_client.customer_id;
                cm.lastseenonline = DateTime.Now; //ignoring time sent from the device
                cm.last_ip = client_ip; //Overwrite client IP by the one provided by the server
                cm.memory = _ro.memory;
                cm.cpu = _ro.cpu;
                cm.free_disk = _ro.free_disk;
                cm.uptime = _ro.uptime;
                cm.network_load = 0; //ignoring network load attribute for now
                dbc.ClientMetrics.Add(cm);

                Client cl = dbc.Clients.First(s => s.sn == this_client.sn);
                cl.lastseenonline = cm.lastseenonline;
                if (cl.status == "provisioned") cl.status = "active";
                dbc.Clients.Update(cl);

                dbc.SaveChanges();
            }
            catch
            {
                Debug.WriteLine("UpdateClientMetrics Method-> Couldn't add data to DB");
            }
        }
    }
}
