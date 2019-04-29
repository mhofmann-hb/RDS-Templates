﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using MSFT.WVD.Monitoring.Common.BAL;
using MSFT.WVD.Monitoring.Common.Models;
using MSFT.WVD.Monitoring.Common.Services;
using MSFT.WVD.Monitoring.Models;
using Newtonsoft.Json;

namespace MSFT.WVD.Monitoring.Controllers
{
    public class DiagonizeIssuesController : Controller
    {
        private readonly ILogger _logger;
        DiagnozeService _diagnozeService;
        UserService _userService;
        UserSessionService _userSessionService;
        DiagnosticActivitityBL diagnosticActivityBL = new DiagnosticActivitityBL();
        private readonly HttpClient apiClient;
        public DiagonizeIssuesController(ILogger<DiagonizeIssuesController> logger, DiagnozeService diagnozeService, UserSessionService userSessionService , UserService userService)
        {
            _logger = logger;
            _diagnozeService = diagnozeService;
            _userSessionService = userSessionService;
            _userService = userService;
            apiClient = new HttpClient();
            apiClient.Timeout = TimeSpan.FromMinutes(30);
            apiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        public IActionResult Index()
        {
            //var role = HttpContext.Session.Get<IEnumerable<RoleAssignment>>("WVDRoles").FirstOrDefault();
            var role = HttpContext.Session.Get<RoleAssignment>("SelectedRole");
            var viewData = new DiagonizePageViewModel() { SelectedRole = role, DiagonizeQuery = new DiagonizeQuery() { StartDate = DateTime.Now.AddDays(-2), EndDate = DateTime.Now } };
            return View(viewData);
        }

        [HttpPost]
        public async Task<IActionResult> Index(DiagonizePageViewModel data)
        {

            var viewData = new DiagonizePageViewModel();
            if (ModelState.IsValid)
            {
                UserInfo userInfo= _userService.GetUserData();
                string tenantGroupName = userInfo.tenantGroupName; //HttpContext.Session.Get<string>("SelectedTenantGroupName");
                string tenant = userInfo.tenant; // HttpContext.Session.Get<string>("SelectedTenantName");
                string accessToken = await HttpContext.GetTokenAsync("access_token");
                //apiClient.BaseAddress = new Uri($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/api/");
                //apiClient.DefaultRequestHeaders.Add("Authorization", accessToken);
                if (data.DiagonizeQuery.ActivityType == ActivityType.Management)
                {
                    _logger.LogInformation($"Call api to get management activity details for selected tenant group {tenantGroupName} and tenant {tenant}");


                    //call from service layer
                    viewData.ManagementActivity = await _diagnozeService.GetManagementActivities(accessToken, data.DiagonizeQuery.UPN, tenantGroupName, tenant, data.DiagonizeQuery.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.EndDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.ActivityOutcome.ToString()).ConfigureAwait(false);
                    if (viewData.ManagementActivity?.Count > 0 && viewData.ManagementActivity[0].ErrorDetails != null)
                    {
                        _logger.LogError($"Error Occured : {viewData.ManagementActivity[0].ErrorDetails.Message}");
                        return RedirectToAction("Error", "Home", new ErrorDetails() { Message = viewData.ManagementActivity[0].ErrorDetails.Message });
                    }
                    viewData.ActivityType = viewData.ManagementActivity?.Count() > 0 ? ActivityType.Management : ActivityType.None;
                }
                else if (data.DiagonizeQuery.ActivityType == ActivityType.Connection)
                {
                    _logger.LogInformation($"Call api to get connection activity details for selected tenant group {tenantGroupName} and tenant {tenant}");

                    //call from service layer
                    viewData.ConnectionActivity = await _diagnozeService.GetConnectionActivities(accessToken, data.DiagonizeQuery.UPN, tenantGroupName, tenant, data.DiagonizeQuery.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.EndDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.ActivityOutcome.ToString()).ConfigureAwait(false);
                    if (viewData.ConnectionActivity?.Count > 0 && viewData.ConnectionActivity[0].ErrorDetails != null)
                    {
                        _logger.LogError($"Error Occured : {viewData.ConnectionActivity[0].ErrorDetails.Message}");
                        return RedirectToAction("Error", "Home", new ErrorDetails() { Message = viewData.ConnectionActivity[0].ErrorDetails.Message });
                    }

                    viewData.ActivityType = viewData.ConnectionActivity?.Count() > 0 ? ActivityType.Connection : ActivityType.None;
                }
                else if (data.DiagonizeQuery.ActivityType == ActivityType.Feed)
                {
                    viewData.FeedActivity = await _diagnozeService.GetFeedActivities(accessToken, data.DiagonizeQuery.UPN, tenantGroupName, tenant, data.DiagonizeQuery.StartDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.EndDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), data.DiagonizeQuery.ActivityOutcome.ToString()).ConfigureAwait(false);
                    if (viewData.FeedActivity?.Count > 0 && viewData.FeedActivity[0].ErrorDetails != null)
                    {
                        _logger.LogError($"Error Occured : {viewData.FeedActivity[0].ErrorDetails.Message}");
                        return RedirectToAction("Error", "Home", new ErrorDetails() { Message = viewData.FeedActivity[0].ErrorDetails.Message });
                    }
                    viewData.ActivityType = viewData.FeedActivity?.Count() > 0 ? ActivityType.Feed : ActivityType.None;
                }
            }
            HttpContext.Session.Set<DiagonizeQuery>("SearchQuery", data.DiagonizeQuery);
            viewData.DiagonizeQuery = data.DiagonizeQuery;
            return View("SearchResults", viewData);
        }

        public async Task<IActionResult> SearchResults()
        {
            var searchQuery = HttpContext.Session.Get<DiagonizeQuery>("SearchQuery");
            RoleAssignment role = HttpContext.Session.Get<RoleAssignment>("SelectedRole");
            if (searchQuery != null)
            {
                return await Index(new DiagonizePageViewModel() { SelectedRole = role, DiagonizeQuery = searchQuery });
            }
            else
            { return RedirectToAction("Index"); }
        }
        public IActionResult GetUserSessions(DiagnoseDetailPageViewModel data)
        {
            return View("UserSessions", data);
        }

        public async Task<List<UserSession>> GetUserSessions(string accessToken, string tenantGroupName, string tenant, string hostPoolName, string hostName)
        {

           var data = await  _userSessionService.GetUserSessions(accessToken, tenantGroupName, tenant, hostPoolName, hostName);
            return data;

        }


        [HttpPost]
        public async Task<IActionResult> LogOffUserSession(DiagnoseDetailPageViewModel data)
        {
            var viewData = new LogOffUserQuery();
            string accessToken = await HttpContext.GetTokenAsync("access_token");
            string tenantGroupName = HttpContext.Session.Get<string>("SelectedTenantGroupName");
            string tenant = HttpContext.Session.Get<string>("SelectedTenantName");
            List<MessageStatus> messageStatus = new List<MessageStatus>();

           
                if (data.UserSessions.Where(x => x.IsSelected == true).ToList().Count > 0)
                {
                    foreach (var item in data.UserSessions.Where(x => x.IsSelected == true).ToList())
                    {
                        LogOffUserQuery logOffUserQuery = new LogOffUserQuery()
                        {
                            tenantGroupName = item.tenantGroupName,
                            tenantName = item.tenantName,
                            hostPoolName = item.hostPoolName,
                            sessionHostName = item.sessionHostName,
                            sessionId = item.sessionId
                        };
                        var Content = new StringContent(JsonConvert.SerializeObject(logOffUserQuery), Encoding.UTF8, "application/json");
                        _logger.LogInformation($"Call api to log off user session ");
                       // var response = await client.PostAsync($"SessionHost/LogOffUser", Content);
                    var response = await _userSessionService.LogOffUserSession(accessToken, logOffUserQuery);
                        if (response == "Ok" || response == "Success")
                        {
                            messageStatus.Add(new MessageStatus()
                            {
                                Message = $"Log off sucessfully for {item.userPrincipalName} user session.",
                                Status = "Success"
                            });
                        }
                        else
                        {
                            messageStatus.Add(new MessageStatus()
                            {
                                Message = $"Failed to logoff {item.userPrincipalName} user session.",
                                Status = "Error"
                            });
                        }
                    }
                }
                else
                {
                    messageStatus.Add(new MessageStatus()
                    {
                        Message = $"Please select users.",
                        Status = "Error"
                    });
                }
            
            return View("ActivityHostDetails", new DiagnoseDetailPageViewModel()
            {
                Title = "",
                Message = "",
                SendMsgStatuses = messageStatus,
                ConnectionActivity = data.ConnectionActivity,
                ShowConnectedUser = true,
                ShowMessageForm = false,
                UserSessions = await GetUserSessions(accessToken, tenantGroupName, tenant, data.ConnectionActivity.SessionHostPoolName, data.ConnectionActivity.SessionHostName)
            });
        }


        public async Task<IActionResult> ShowMessagePanel(DiagnoseDetailPageViewModel data)
        {

            string accessToken = await HttpContext.GetTokenAsync("access_token");
            string tenantGroupName = HttpContext.Session.Get<string>("SelectedTenantGroupName");
            string tenant = HttpContext.Session.Get<string>("SelectedTenantName");
            return View("ActivityHostDetails", new DiagnoseDetailPageViewModel()
            {
                Title = "",
                Message = "",
                ConnectionActivity = data.ConnectionActivity,
                ShowConnectedUser = true,
                ShowMessageForm = true,
                UserSessions = await GetUserSessions(accessToken, tenantGroupName, tenant, data.ConnectionActivity.SessionHostPoolName, data.ConnectionActivity.SessionHostName)
            });




        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(DiagnoseDetailPageViewModel data)
        {

            if (ModelState.IsValid)
            {
                var viewData = new SendMessageQuery();
                List<MessageStatus> messageStatus = new List<MessageStatus>();
                string accessToken = await HttpContext.GetTokenAsync("access_token");
                string tenantGroupName = HttpContext.Session.Get<string>("SelectedTenantGroupName");
                string tenant = HttpContext.Session.Get<string>("SelectedTenantName");
               
                    if (data.UserSessions.Where(x => x.IsSelected == true).ToList().Count > 0)
                    {
                        foreach (var item in data.UserSessions.Where(x => x.IsSelected == true).ToList())
                        {
                            SendMessageQuery sendMessageQuery = new SendMessageQuery()
                            {
                                tenantGroupName = item.tenantGroupName,
                                tenantName = item.tenantName,
                                hostPoolName = item.hostPoolName,
                                sessionHostName = item.sessionHostName,
                                sessionId = item.sessionId,
                                messageTitle = data.Title,
                                messageBody = data.Message,
                                userPrincipalName = item.userPrincipalName
                            };
                            var Content = new StringContent(JsonConvert.SerializeObject(sendMessageQuery), Encoding.UTF8, "application/json");
                            _logger.LogInformation($"Call api to send message to {item.userPrincipalName}");
                            var response =  await _userSessionService.SendMessage(accessToken, sendMessageQuery);
                        if (response == HttpStatusCode.OK.ToString() )
                        {
                                messageStatus.Add(new MessageStatus()
                                {
                                    Message = $"Message sent sucessfully to {item.userPrincipalName}",
                                    Status = "Success"
                                });
                                data.Title = data.Message = "";
                            }
                            else
                            {
                                messageStatus.Add(new MessageStatus()
                                {
                                    Message = $"Failed to send message to {item.userPrincipalName}",
                                    Status = "Error"
                                });
                                data.Title = data.Message = "";
                            }
                        }
                    }
                    else
                    {
                        messageStatus.Add(new MessageStatus()
                        {
                            Message = $"Please select users",
                            Status = "Error"
                        });
                    }


                    return View("ActivityHostDetails", new DiagnoseDetailPageViewModel()
                    {
                        UserSessions = data.UserSessions.Where(usr => usr.IsSelected = true)
               .Select(usr => { usr.IsSelected = false; return usr; })
               .ToList(),
                        Title = data.Title,
                        Message = data.Message,
                        SendMsgStatuses = messageStatus,
                        ConnectionActivity = data.ConnectionActivity,
                        ShowConnectedUser = true,
                        ShowMessageForm = true
                    });
               
            }
            else
            {
                return View("ActivityHostDetails", new DiagnoseDetailPageViewModel()
                {
                    UserSessions = data.UserSessions.Where(usr => usr.IsSelected = true)
              .Select(usr => { usr.IsSelected = false; return usr; })
              .ToList(),
                    ConnectionActivity = data.ConnectionActivity,
                    ShowConnectedUser = true,
                    ShowMessageForm = true,
                    Title = data.Title,
                    Message = data.Message,
                });
            }

        }

        public async Task<IActionResult> ActivityHostDetails(string id)
        {
            string accessToken = await HttpContext.GetTokenAsync("access_token");
            string tenantGroupName = HttpContext.Session.Get<string>("SelectedTenantGroupName");
            string tenant = HttpContext.Session.Get<string>("SelectedTenantName");


            apiClient.BaseAddress = new Uri($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/api/");
            apiClient.DefaultRequestHeaders.Add("Authorization", accessToken);
            HttpResponseMessage response = await apiClient.GetAsync($"DiagnosticActivity/GetActivityDetails/?tenantGroupName={tenantGroupName}&tenant={tenant}&activityId={id}");
            if (response.IsSuccessStatusCode)
            {
                var strConnectionDetails = response.Content.ReadAsStringAsync().Result;
                var ConnectionActivity = JsonConvert.DeserializeObject<List<ConnectionActivity>>(strConnectionDetails);
                List<UserSession> userSessions = await GetUserSessions(accessToken, tenantGroupName, tenant, ConnectionActivity[0].SessionHostPoolName, ConnectionActivity[0].SessionHostName);
                ViewBag.ShowConnectedUser = true;

                return View(new DiagnoseDetailPageViewModel()
                {
                    ConnectionActivity = new ConnectionActivity
                    {
                        activityId = ConnectionActivity[0].activityId,
                        activityType = ConnectionActivity[0].activityType,
                        outcome = ConnectionActivity[0].outcome,
                        Tenants = ConnectionActivity[0].Tenants,
                        userName = ConnectionActivity[0].userName,
                        ClientOS = ConnectionActivity[0].ClientOS,
                        ClientIPAddress = ConnectionActivity[0].ClientIPAddress,
                        startTime = Convert.ToDateTime(ConnectionActivity[0].startTime),
                        endTime = Convert.ToDateTime(ConnectionActivity[0].endTime),
                        SessionHostName = ConnectionActivity[0].SessionHostName,
                        SessionHostPoolName = ConnectionActivity[0].SessionHostPoolName
                    },
                    UserSessions = userSessions,
                    ShowConnectedUser = false,
                    ShowMessageForm = true
                });
            }
            else
            {
                return RedirectToAction("Error", "Home", new ErrorDetails() { Message = response.Content.ReadAsStringAsync().Result, StatusCode = (int)response.StatusCode });
            }
        }
    }
}