using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Nancy.Json;
using System.IO;
using System.Text;
using SERVAPI.Models;
using System.Diagnostics;
using System.Linq;

namespace SERVAPI
{

    public class Router
    {
        private ServapiDBContext db;
        private string clientip;
        private Edge.RequestSnapshot ro;
        private Edge.StandardRequest data1;
        private DBClientData myclientdata;

        public Router()
        {
            db = new ServapiDBContext();
            //Debug.WriteLine(new JavaScriptSerializer().Serialize(db.Clients.ToList()));
        }
        public async Task<HttpResponseMessage> RouteRequest(HttpRequest request, string _clientip)
        {
            clientip = _clientip;
            try
            {
                ro = new Edge.RequestSnapshot(request);
                data1 = Edge.ExtractStandardRequest(ro.uri);
                myclientdata = new DBClientData(data1.sn, db); //Load client data from DB
                if ((ro.uri.Length > 5) && (data1!=null) && (myclientdata!=null) && (myclientdata.this_client != null))
                {
                       
                    myclientdata.UpdateJournal("NEW CLIENT CONNECTION: "+request.Method + ro.path + " FROM " + _clientip, new JavaScriptSerializer().Serialize(data1));
                    
                    if (request.Method == "GET")
                    {
                        //Parsing HTTP GET request path
                        switch (ro.path)
                        {
                            case "/hello": //Hello type request
                                myclientdata.UpdateClientMetrics(data1, _clientip);
                                return ProcessEdgeHello();
                            case "/report": //Failure report type request
                                return ProcessEdgeReport();
                            case "/confirm": //Confirm report type request
                                return ProcessEdgeConfirm();
                            case "/update": //Update result type request
                                return ProcessEdgeUpdate();
                            default:  //Unidentified request
                                return ConstructErrorMessage("WRONG PATH IN HTTP REQUEST HEADER");
                        }
                    }
                    else if (request.Method == "POST")
                    {
                       
                        //Parsing HTTP POST request path
                        switch (ro.path)
                        {
                            case "/command": //Command result type request
                                return await ProcessEdgeCommandAsync(request);
                            case "/upload": //Upload type request
                                return await ProcessEdgeUploadAsync(request);
                            default:  //Unidentified request
                                return ConstructErrorMessage("WRONG PATH IN HTTP REQUEST HEADER");
                        }
                    }
                    else
                    {
                        return ConstructErrorMessage("HTTP METHOD IS UNSUPPORTED");
                    }
                }
                else
                {
                    return ConstructErrorMessage("WRONG OR MISSING URI OR CORRUPTED CLIENT DATA IN DB");
                }

                }
            catch
            {
                return ConstructErrorMessage("REQUEST PROCESSING FAILED");
            }

        }



        private HttpResponseMessage ProcessEdgeHello()
        {
            //////////ОБРАБОТКА HELLO!
            //Загружаем очередь клиента, обрабатываем первую запись.
            DBQueueData resp_data = new DBQueueData(myclientdata.this_client.sn, myclientdata.this_client.customer_id, db);
            if (resp_data._lastcommand == "command")
            {
                Edge.Response_Command _resp1 = resp_data.PrepareCommand();
                System.Diagnostics.Debug.WriteLine(new JavaScriptSerializer().Serialize(_resp1));
                return ConstructOKMessage(new JavaScriptSerializer().Serialize(_resp1));
            }
            else if (resp_data._lastcommand == "update")
            {
                Edge.Response_Update _resp1 = resp_data.PrepareUpdate();
                System.Diagnostics.Debug.WriteLine(new JavaScriptSerializer().Serialize(_resp1));
                return ConstructOKMessage(new JavaScriptSerializer().Serialize(_resp1));
            }
            else if (resp_data._lastcommand == "settings")
            {
                Edge.Response_Settings _resp1 = resp_data.PrepareSettings();
                System.Diagnostics.Debug.WriteLine(new JavaScriptSerializer().Serialize(_resp1));
                return ConstructOKMessage(new JavaScriptSerializer().Serialize(_resp1));
            }
            else
            {
                //DO NOTHING -> no commands in the queue
                Edge.Response_Standby _resp1 = new Edge.Response_Standby();
                System.Diagnostics.Debug.WriteLine(new JavaScriptSerializer().Serialize(_resp1));
                return ConstructOKMessage(new JavaScriptSerializer().Serialize(_resp1));
            }
        }

        private HttpResponseMessage ProcessEdgeReport()
        {
            System.Diagnostics.Debug.WriteLine("ProcessEdgeReport Method -> Failure report received from " + data1.sn + " for task id " + data1.id);
            
            if (data1.id != null)
            {
                DBQueueData resp_data = new DBQueueData(myclientdata.this_client.sn, myclientdata.this_client.customer_id, db);
                resp_data.AddErrorToTask(DateTime.Now.ToString() + " - Failure report received from client. -> " + data1.custom_field);
            } else
            {
                myclientdata.UpdateJournal("Client reported error", data1.custom_field);
            }
                return ConstructOKMessage("Report accepted.");           
        }

        private HttpResponseMessage ProcessEdgeConfirm()
        {
            System.Diagnostics.Debug.WriteLine("Confirmation report received from " + data1.sn + " for task " + data1.id);
            DBQueueData resp_data = new DBQueueData(myclientdata.this_client.sn, myclientdata.this_client.customer_id, db);
            System.Diagnostics.Debug.WriteLine("POSTT->ProcessEdgeConfirm Method->-Write received file to client task log");
            bool res = false;
                System.Diagnostics.Debug.WriteLine("POSTT->ProcessEdgeConfirm Method->-UpdateQueue(copy task to processed dir)");
                if (resp_data.UpdateQueue(Convert.ToInt64(data1.id),null) == true) 
                {
                    res = true;
                    /*
                    if (resp_data._lastloadedsettings!=null) //If last command was "settings" we need to update client settings on the server
                    {
                        System.Diagnostics.Debug.WriteLine("POSTT->ProcessEdgeConfirm Method->-Saving new client settings");
                        myclientdata.UpdateClientData(resp_data._lastloadedsettings);
                    } else
                    {
                        System.Diagnostics.Debug.WriteLine("POSTT->ProcessEdgeConfirm Method->-Client settings were not updated on the server");
                    }*/
                }
            

            if (res == true)
            {
                System.Diagnostics.Debug.WriteLine("POSTT->ProcessEdgeConfirm Method-> "+ data1.id + " task (change settings) was completed by the client.");
                return ConstructOKMessage("OK");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Router.ProcessEdgeConfirm-> Method didn't complete due to error.");
                resp_data.AddErrorToTask(DateTime.Now.ToString() + " - Error while processing confirmation from client.");
                return ConstructErrorMessage("Router.ProcessEdgeConfirm-> Error while writing log file - ");
            }
        }

        private async Task<HttpResponseMessage> ProcessEdgeCommandAsync(HttpRequest request)
        {
            System.Diagnostics.Debug.WriteLine("POST->ProcessEdgeCommand Method execution");
            System.Diagnostics.Debug.WriteLine("POST->ProcessEdgeCommand Method->Initializing DBData");
            bool res = false;
            DBQueueData resp_data = new DBQueueData(myclientdata.this_client.sn, myclientdata.this_client.customer_id, db);
            try
            {
                using (StreamReader reader = new StreamReader(request.Body, Encoding.ASCII))
                {
                    System.Diagnostics.Debug.WriteLine("POST->ProcessEdgeCommand Method->reading file stream from body");
                    var k = await reader.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine("POST->ProcessEdgeCommand Method->-UpdateQueue(copy task to processed dir)");
                    if (resp_data.UpdateQueue(Convert.ToInt64(data1.id),k) == true)
                    {
                        res = true;
                    }
                }
            } catch
            {
                System.Diagnostics.Debug.WriteLine("Router.ProcessEdgeCommand-> Error! File couldn't be obtained from the client or couldn't be saved!");
                resp_data.AddErrorToTask("Router.ProcessEdgeCommand-> Error! File couldn't be obtained from the client or couldn't be saved!");
                return ConstructErrorMessage("Router.ProcessEdgeCommand-> Error! File couldn't be obtained from the client or couldn't be saved!");
            }

            if (res == true)
            {
                System.Diagnostics.Debug.WriteLine("POST->ProcessEdgeCommand Method-> " + data1.id + " task (change settings) was completed by the client.");
                return ConstructOKMessage("OK");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Router.ProcessEdgeCommand-> Method didn't complete due to error.");
                resp_data.AddErrorToTask(DateTime.Now.ToString() + " - Error while processing command output in client's reply.");
                return ConstructErrorMessage("Router.ProcessEdgeCommand-> Method didn't complete due to error.");
            }

        }

        private HttpResponseMessage ProcessEdgeUpdate()
        {
            System.Diagnostics.Debug.WriteLine("GET->ProcessEngeUpdate Method execution - Phase1 - Files transfer to the client");
            DBQueueData resp_data = new DBQueueData(myclientdata.this_client.sn, myclientdata.this_client.customer_id, db);
            Edge.Response_Update resp_edge = resp_data.PrepareUpdate();
            System.Diagnostics.Debug.WriteLine("GET->ProcessEngeUpdate Method execution->forming multipart response body");
            MultipartContent multipartContent = resp_data.GetFilesFromDB(resp_edge);
            if (multipartContent != null)
            {
                HttpResponseMessage response = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = multipartContent
                };
                return response;
            } else
            {
                System.Diagnostics.Debug.WriteLine("Router.ProcessEdgeUpdate-> Error in ProcessEngeUpdate Method-> Something went wrong.");
                resp_data.AddErrorToTask(DateTime.Now.ToString() + " - Error while uploading files to client.");
                return ConstructErrorMessage("Router.ProcessEdgeUpdate-> Error in ProcessEngeUpdate Method-> Something went wrong.");
            }

           
        }

        private async Task<HttpResponseMessage> ProcessEdgeUploadAsync(HttpRequest request)
        {
            bool res = false;
            System.Diagnostics.Debug.WriteLine("ProcessEdgeUpload Method -> Reading file stream from HTTP request");
            try
            {
                //////////////////Добавить чтение номера таска, чтобы объединять файлы в один таск.
                StreamReader reader1 = new StreamReader(request.Body);
                //var k = await reader1.ReadToEndAsync();
                MemoryStream ms = new MemoryStream();
                await reader1.BaseStream.CopyToAsync(ms);
                if (myclientdata.SaveUploadedFile(ms.ToArray(), data1.filetype, data1.filename)>0)
                {
                    res = true;
                    System.Diagnostics.Debug.WriteLine("ProcessEdgeUpload Method -> Client data was saved: " + data1.filename);
                }
            } catch
            {
                System.Diagnostics.Debug.WriteLine("ProcessEdgeUpload Method -> Error. Could not load file from client: " + data1.filename);
            }
            if (res == true)
            {
                return ConstructOKMessage("OK. File received.");
            } else
            {
                return ConstructErrorMessage("ProcessEdgeUpload-> File upload task wasn't completed.");
            }
        }

        private HttpResponseMessage ConstructErrorMessage(string error)
        {
            if (myclientdata != null)
            {
                myclientdata.UpdateJournal("WEBSERVER EXCEPTION", error);
            }
            
            HttpResponseMessage errorMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent(error)
            };
            return errorMessage;
        }
        private HttpResponseMessage ConstructOKMessage(string message)
        {
            HttpResponseMessage errorMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(message)
            };
            return errorMessage;
        }

    }
}
