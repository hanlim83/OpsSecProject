using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleEmail;
using OpsSecProject.Areas.Internal.Data;
using System;
using Amazon.SimpleEmail.Model;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace OpsSecProject.Controllers
{
    public class AccountController : Controller
    {

        private readonly AuthenticationContext _context;
        private readonly IAmazonSimpleNotificationService _snsClient;
        private readonly IAmazonSimpleEmailService _sesClient;

        public AccountController(AuthenticationContext context, IAmazonSimpleNotificationService snsClient, IAmazonSimpleEmailService sesClient)
        {
            _context = context;
            _snsClient = snsClient;
            _sesClient = sesClient;
        }

        public async Task<IActionResult> Index()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _context.Users.FindAsync(currentIdentity);
            AccountOverallViewModel model = new AccountOverallViewModel
            {
                User = user,
                Useractivites = await _context.Activities.Where(a => a.LinkedUser == user).ToListAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> Profile()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User currentUser = await _context.Users.FindAsync(currentIdentity);
            return View(currentUser);
        }

        public async Task<IActionResult> Settings()
        {
            return View();
        }

        public async Task<IActionResult> Activity()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
            User user = await _context.Users.FindAsync(currentIdentity);
            return View(await _context.Activities.Where(a => a.LinkedUser == user).ToListAsync());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Unauthorised()
        {
            ViewData["RequestID"] = HttpContext.TraceIdentifier;
            return View();
        }

        public IActionResult Claims()
        {
            ViewData["User"] = HttpContext.User;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            string authMethod = claimsIdentity.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider").Value;
            if (authMethod.Equals("https://smartinsights.hansen-lim.me"))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Signout", "Landing");
            }
            else if (!authMethod.Equals("https://smartinisights.hansen-lim.me"))
            {
                foreach (var cookieKey in HttpContext.Request.Cookies.Keys)
                {
                    HttpContext.Response.Cookies.Delete(cookieKey);
                }
                return RedirectToAction("Logout", "Landing");
            }
            else
            {
                return StatusCode(500);
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Manage()
        {
            UsersOverallManagementViewModel model = new UsersOverallManagementViewModel
            {
                allUsers = await _context.Users.ToListAsync(),
                allRoles = await _context.Roles.ToListAsync()
            };
            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> CreateUser()
        {
            UserDataManagementViewModel model = new UserDataManagementViewModel
            {
                allRoles = await _context.Roles.ToListAsync()
            };
            return View(model);
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> CreateUser([Bind("Username", "Name", "PhoneNumber", "EmailAddress", "Role")]UserDataManagementViewModel newUser)
        {
            if (newUser.PhoneNumber == null && newUser.EmailAddress == null)
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "You must specify either a Phone Number or Email Address";
                newUser.allRoles = await _context.Roles.ToListAsync();
                return View(newUser);
            }
            else
            {
                User addition = new User
                {
                    Username = newUser.Username,
                    Name = newUser.Name,
                    Existence = Existence.Internal,
                    Password = Password.GetRandomSalt(),
                    Status = Status.Pending,
                    OverridableField = OverridableField.Both
                };
                if (!newUser.Role.Equals("User"))
                {
                    Role role = await _context.Roles.FindAsync(newUser.Role);
                    addition.LinkedRole = role;
                }
                if (newUser.PhoneNumber == null)
                    addition.EmailAddress = newUser.EmailAddress;
                else
                    addition.PhoneNumber = newUser.PhoneNumber;
                _context.Users.Add(addition);
                try
                {
                    await _context.SaveChangesAsync();
                } catch (DbUpdateException)
                {
                    ViewData["Alert"] = "Danger";
                    ViewData["Message"] = "Something went wrong. Maybe try again?";
                    return View(newUser);
                }
                addition = await _context.Users.FindAsync(newUser.Username);
                NotificationToken token = new NotificationToken
                {
                    Type = Models.Type.Activate,
                    Vaild = true,
                    LinkedUser = addition
                };
                if (addition.EmailAddress != null)
                {
                    token.Token = Areas.Internal.Controllers.AccountController.TokenGenerator();
                    SendEmailRequest SESrequest = new SendEmailRequest
                    {
                        Source = Environment.GetEnvironmentVariable("SES_EMAIL_FROM-ADDRESS"),
                        Destination = new Destination
                        {
                            ToAddresses = new List<string>
                        {
                            addition.EmailAddress
                        }
                        },
                        Message = new Message
                        {
                            Subject = new Content("Welcome to SmartInsights"),
                            Body = new Body
                            {
                                Text = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = "Hi " + addition.Name + ",\r\n\n" + HttpContext.User.Claims.First(c => c.Type == "name").Value + " has created an account for you on SmartInsights. Your username to login is:\r\n" + addition.Username + "\r\n\nTo enable your account, you will need to set your password and verify this email address. Please click on this link: " + "https://" + HttpContext.Request.Host + "/Internal/Account/SetPassword?token=" + token.Token + " to do so.\r\n\n\nThis is a computer-generated email, please do not reply"
                                }
                            }
                        }
                    };
                    SendEmailResponse response = await _sesClient.SendEmailAsync(SESrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.EMAIL;
                }
                else
                {
                    PublishRequest SNSrequest = new PublishRequest
                    {
                        Message = HttpContext.User.Claims.First(c => c.Type == "name").Value + " has created an account for you on SmartInsights. Your username to login is: " + addition.Username + ". Please click on this link to set your password and verify this phone number: " + "https://" + HttpContext.Request.Host + "/Internal/Account/SetPassword?token=" + token.Token,
                        PhoneNumber = "+65" + addition.PhoneNumber
                    };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartIS", DataType = "String" };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                    PublishResponse response = await _snsClient.PublishAsync(SNSrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.SMS;
                }
                await _context.NotificationTokens.AddAsync(token);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Succesfully created " + addition.Name + "'s account. Please ask " + addition.Name + " to look at the email/SMS to activate the account";
                TempData["Alert"] = "Success";
                return RedirectToAction("Manage");
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditUser(string Username)
        {
            User identity = await _context.Users.FindAsync(Username);
            if (identity == null)
                return StatusCode(404);
            else
            {
                UserDataManagementViewModel model = new UserDataManagementViewModel
                {
                    user = identity,
                    allRoles = await _context.Roles.ToListAsync()
                };
                if (identity.LinkedRole == null)
                    model.Role = "User";
                return View(model);
            }
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> EditUser([Bind("Username", "Name", "PhoneNumber", "EmailAddress", "Role")]UserDataManagementViewModel existingUser)
        {
            bool change = false;
            User identity = await _context.Users.FindAsync(existingUser.Username);
            if (identity == null)
                return StatusCode(404);
            else if (existingUser.PhoneNumber == null && existingUser.EmailAddress == null)
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "You must specify either a Phone Number or Email Address";
                existingUser.user = identity;
                existingUser.allRoles = await _context.Roles.ToListAsync();
                return View(existingUser);
            }
            else
            {
                NotificationToken token = new NotificationToken
                {
                    Type = Models.Type.Verify,
                    Vaild = true,
                    LinkedUser = identity
                };
                if (identity.Existence == Existence.Internal && !existingUser.Name.Equals(identity.Name))
                {
                    identity.Name = existingUser.Name;
                    change = true;
                }
                if (!existingUser.Role.Equals("User") && identity.Existence == Existence.Internal)
                {
                    Role role = await _context.Roles.FindAsync(existingUser.Role);
                    if (identity.LinkedRole != role)
                    {
                        identity.LinkedRole = role;
                        change = true;
                    }
                }
                else if (existingUser.Role.Equals("User") && identity.Existence == Existence.Internal && identity.LinkedRole != null)
                {
                    identity.LinkedRole = null;
                    change = true;
                }
                if (existingUser.PhoneNumber != null && (identity.PhoneNumber == null || !identity.PhoneNumber.Equals(existingUser.PhoneNumber)) && (identity.OverridableField == OverridableField.PhoneNumber || identity.OverridableField == OverridableField.Both))
                {
                    identity.PhoneNumber = existingUser.PhoneNumber;
                    identity.VerifiedPhoneNumber = false;
                    token.Token = Areas.Internal.Controllers.AccountController.TokenGenerator();
                    PublishRequest SNSrequest = new PublishRequest
                    {
                        Message = HttpContext.User.Claims.First(c => c.Type == "name").Value + " has changed the phone number on your account. To confirm this change, please click on this link: " + "https://" + HttpContext.Request.Host + "/Internal/Account/VerifyPhoneNumber?token=" + token.Token,
                        PhoneNumber = "+65" + identity.PhoneNumber
                    };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartIS", DataType = "String" };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                    PublishResponse response = await _snsClient.PublishAsync(SNSrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.SMS;
                    _context.NotificationTokens.Add(token);
                    change = true;
                }
                else if (existingUser.PhoneNumber == null && identity.PhoneNumber != null && (identity.OverridableField == OverridableField.PhoneNumber || identity.OverridableField == OverridableField.Both))
                {
                    identity.PhoneNumber = null;
                    identity.VerifiedPhoneNumber = false;
                    change = true;
                }
                if (existingUser.EmailAddress != null && (identity.EmailAddress == null || !identity.EmailAddress.Equals(existingUser.EmailAddress)) && (identity.OverridableField == OverridableField.EmailAddress || identity.OverridableField == OverridableField.Both))
                {
                    identity.EmailAddress = existingUser.EmailAddress;
                    identity.VerifiedEmailAddress = false;
                    token.Token = Areas.Internal.Controllers.AccountController.TokenGenerator();
                    SendEmailRequest SESrequest = new SendEmailRequest
                    {
                        Source = Environment.GetEnvironmentVariable("SES_EMAIL_FROM-ADDRESS"),
                        Destination = new Destination
                        {
                            ToAddresses = new List<string>
                        {
                            identity.EmailAddress
                        }
                        },
                        Message = new Message
                        {
                            Subject = new Content("Verify your email address for SmartInsights"),
                            Body = new Body
                            {
                                Text = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = "Hi " + identity.Name + ",\r\n\n" + HttpContext.User.Claims.First(c => c.Type == "name").Value + " has changed the email address on your account. To confirm this change, please click on this link: " + "https://" + HttpContext.Request.Host + "/Internal/Account/VerifyEmailAddress?token=" + token.Token + "\r\n\n\nThis is a computer-generated email, please do not reply"
                                }
                            }
                        }
                    };
                    SendEmailResponse response = await _sesClient.SendEmailAsync(SESrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.EMAIL;
                    _context.NotificationTokens.Add(token);
                    change = true;
                }
                else if (existingUser.EmailAddress == null && identity.EmailAddress != null && (identity.OverridableField == OverridableField.EmailAddress || identity.OverridableField == OverridableField.Both))
                {
                    identity.EmailAddress = null;
                    identity.VerifiedEmailAddress = false;
                    change = true;
                }
                _context.Users.Update(identity);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    ViewData["Alert"] = "Danger";
                    ViewData["Message"] = "Something went wrong. Maybe try again?";
                    return View(existingUser);
                }
                if (change)
                {
                    TempData["Message"] = "Succesfully edited " + identity.Name + "'s account details";
                    TempData["Alert"] = "Success";
                }
                else
                {
                    TempData["Message"] = "No changes made to " + identity.Name + "'s account details";
                    TempData["Alert"] = "Warning";
                }
                return RedirectToAction("Manage");
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ChangeUserStatus(string Username)
        {
            User identity = await _context.Users.FindAsync(WebUtility.HtmlDecode(Username));
            if (identity == null)
                return StatusCode(404);
            else if (identity.Name.Equals(User.Claims.First(c => c.Type == "name").Value))
                return StatusCode(403);
            else if (identity.Existence == Existence.External)
                return StatusCode(500);
            else
            {
                if (identity.Status == Status.Active)
                {
                    identity.Status = Status.Disabled;
                    TempData["Message"] = "Succesfully disabled " + identity.Name + "'s account";
                }
                else if (identity.Status == Status.Disabled)
                {
                    identity.Status = Status.Active;
                    TempData["Message"] = "Succesfully enabled " + identity.Name + "'s account";
                }
                _context.Users.Update(identity);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    TempData["Alert"] = "Danger";
                    TempData["Message"] = "Something went wrong. Maybe try again?";
                    return RedirectToAction("Manage");
                }
                TempData["Alert"] = "Success";
                return RedirectToAction("Manage");
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RevokeUserSession(string Username)
        {
            User identity = await _context.Users.FindAsync(WebUtility.HtmlDecode(Username));
            if (identity == null)
                return StatusCode(404);
            else if (identity.Name.Equals(User.Claims.First(c => c.Type == "name").Value))
                return StatusCode(403);
            else
            {
                identity.ForceSignOut = true;
                _context.Users.Update(identity);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    TempData["Alert"] = "Danger";
                    TempData["Message"] = "Something went wrong. Maybe try again?";
                    return RedirectToAction("Manage");
                }
                TempData["Alert"] = "Success";
                TempData["Message"] = "Succesfully revoked " + identity.Name + "'s session";
                return RedirectToAction("Manage");
            }
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> RemoveUser(string Username)
        {
            User identity = await _context.Users.FindAsync(WebUtility.HtmlDecode(Username));
            if (identity == null)
                return StatusCode(404);
            else if (identity.Name.Equals(User.Claims.First(c => c.Type == "name").Value))
                return StatusCode(403);
            else
            {
                _context.Users.Remove(identity);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    TempData["Alert"] = "Danger";
                    TempData["Message"] = "Something went wrong. Maybe try again?";
                    return RedirectToAction("Manage");
                }
                TempData["Alert"] = "Success";
                TempData["Message"] = "Succesfully removed " + identity.Name + "'s account";
                return RedirectToAction("Manage");
            }
        }
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditRole(string Role)
        {
            Role role = await _context.Roles.FindAsync(Role);
            if (role == null)
                return StatusCode(404);
            else
            {
                return View(role);
            }
        }
        [Authorize(Roles = "Administrator")]
        [HttpPost]
        public async Task<IActionResult> EditRole([Bind("RoleName", "Existence", "IDPReference")]Role role)
        {
            bool change = false;
            Role modified = await _context.Roles.FindAsync(role.RoleName);
            if (!modified.IDPReference.Equals(role.IDPReference))
            {
                modified.IDPReference = role.IDPReference;
                change = true;
            }
            _context.Roles.Update(modified);
            try
            {
                await _context.SaveChangesAsync();
            } catch (DbUpdateException)
            {
                ViewData["Message"] = "Something went wrong. Maybe try again?";
                ViewData["Alert"] = "Danger";
                return View(modified);
            }
            if (change)
            {
                TempData["Message"] = "Succesfully edited role " + modified.RoleName;
                TempData["Alert"] = "Success";
            }
            else
            {
                TempData["Message"] = "No changes made to role " + modified.RoleName;
                TempData["Alert"] = "Warning";
            }
            return RedirectToAction("Manage");
        }
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Audit()
        {
            return View(await _context.Activities.ToListAsync());
        }
    }
}