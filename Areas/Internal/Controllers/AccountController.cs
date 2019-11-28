﻿using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsSecProject.Areas.Internal.Data;
using OpsSecProject.Areas.Internal.Models;
using OpsSecProject.Data;
using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OpsSecProject.Areas.Internal.Controllers
{
    [Area("Internal")]
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
        [AllowAnonymous]
        public IActionResult SignIn(string ReturnUrl)
        {
            if (ReturnUrl != null)
                ViewData["ReturnURL"] = ReturnUrl;
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SignIn([Bind("Username", "Password", "ReturnUrl")]LoginFormModel Credentials)
        {
            User challenge = _context.Users.Find(Credentials.Username);
            if (challenge == null)
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                Credentials.Password = null;
                return View(Credentials);
            }
            else if (challenge.Existence.Equals(Existence.External))
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "Please login through the identity provider instead";
                Credentials.Password = null;

                return View(Credentials);
            }
            else if (!Password.ValidatePassword(Credentials.Password, challenge.Password))
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                Credentials.Password = null;
                return View(Credentials);
            }
            else
            {
                challenge.ForceSignOut = false;
                challenge.LastAuthentication = DateTime.Now;
                var claims = new List<Claim>{
                    new Claim("name", challenge.Name),
                    new Claim("preferred_username", challenge.Username),
                    new Claim(ClaimTypes.Role, challenge.LinkedRole.RoleName),
                    new Claim("http://schemas.microsoft.com/identity/claims/identityprovider", "https://smartinsights.hansen-lim.me")
                };
                if (challenge.VerifiedPhoneNumber == true)
                    claims.Add(new Claim(ClaimTypes.MobilePhone, challenge.PhoneNumber));
                if (challenge.VerifiedEmail == true)
                    claims.Add(new Claim(ClaimTypes.Email, challenge.EmailAddress));
                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                _context.Update(challenge);
                await _context.SaveChangesAsync();
                if (Credentials.ReturnUrl == null)
                    return Redirect("/Home");
                else
                    return Redirect(Credentials.ReturnUrl);
            }
        }
        [AllowAnonymous]
        public IActionResult ForgetPassword(string ReturnUrl)
        {
            if (ReturnUrl != null)
                ViewData["ReturnURL"] = ReturnUrl;
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgetPassword([Bind("Username", "RedirectUrl")]ForgetPasswordModel User)
        {
            User identity = _context.Users.Find(User.Username);
            if (identity == null)
            {
                ViewData["Alert"] = "Warning";
                ViewData["Message"] = "Invaild Username and Password combination";
                return View(User);
            }
            else if (identity.Existence == Existence.External)
            {
                ViewData["Alert"] = "Danger";
                ViewData["Message"] = "You can't use this function, please login using the identity provider";
                return View(User);
            }
            else if (identity.Existence == Existence.Hybrid)
            {
                var authenticationProperties = new AuthenticationProperties();
                authenticationProperties.Items["prompt"] = "login";
                authenticationProperties.Items["login_hint"] = identity.Username;
                authenticationProperties.RedirectUri = "/Internal/Account/SetPassword";
                return Challenge(authenticationProperties, AzureADDefaults.AuthenticationScheme);
            }
            else
            {
                NotificationToken token = new NotificationToken
                {
                    Type = OpsSecProject.Models.Type.Reset,
                    Vaild = true,
                    LinkedUser = identity
                };
                int length = 30;
                string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
                StringBuilder res = new StringBuilder();
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    byte[] uintBuffer = new byte[sizeof(uint)];

                    while (length-- > 0)
                    {
                        rng.GetBytes(uintBuffer);
                        uint num = BitConverter.ToUInt32(uintBuffer, 0);
                        res.Append(valid[(int)(num % (uint)valid.Length)]);
                    }
                }
                token.Token = res.ToString();
                if (identity.VerifiedEmail == true)
                {
                    SendEmailRequest SESrequest = new SendEmailRequest
                    {
                        Source = "do_not_reply@hansen-lim.me",
                        Destination = new Destination
                        {
                            ToAddresses = new List<string>
                        {
                            identity.EmailAddress
                        }
                        },
                        Message = new Message
                        {
                            Subject = new Content("Reset your password for SmartInsights"),
                            Body = new Body
                            {
                                Text = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = "Hi " + identity.Name + "\r\n\n" + "To complete your password reset request, please click on this link: " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/Internal/Account/ResetPassword?token=" + token.Token + "\r\n\n\nThis email is computer generated, please do not reply this email"
                                }
                            }
                        }
                    };
                    SendEmailResponse response = await _sesClient.SendEmailAsync(SESrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.EMAIL;
                }
                else if (identity.VerifiedPhoneNumber == true)
                {
                    PublishRequest SNSrequest = new PublishRequest
                    {
                        Message = "Please click on this link to complete your password reset request: " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/Internal/Account/ResetPassword?token=" + token.Token,
                        PhoneNumber = identity.PhoneNumber
                    };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue { StringValue = "SmartSights", DataType = "String" };
                    SNSrequest.MessageAttributes["AWS.SNS.SMS.SMSType"] = new MessageAttributeValue { StringValue = "Transactional", DataType = "String" };
                    PublishResponse response = await _snsClient.PublishAsync(SNSrequest);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                        return StatusCode(500);
                    token.Mode = Mode.SMS;
                }
                else
                    return StatusCode(500);
                await _context.NotificationTokens.AddAsync(token);
                await _context.SaveChangesAsync();
                if (token.Mode == Mode.EMAIL)
                    TempData["State"] = "PasswordResetEmail";
                else
                    TempData["State"] = "PasswordResetPhone";
                return RedirectToAction("SignIn", new { RedirectUrl = User.ReturnUrl });
            }
        }
        [AllowAnonymous]
        public async Task<IActionResult> SetPassword(string token)
        {
            if (token == null && !HttpContext.User.Identity.IsAuthenticated)
                return StatusCode(403);
            else if (HttpContext.User.Identity.IsAuthenticated)
            {
                ClaimsIdentity claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
                string currentIdentity = claimsIdentity.FindFirst("preferred_username").Value;
                User currentUser = await _context.Users.FindAsync(currentIdentity);
                if (currentUser.Existence == Existence.Hybrid && currentUser.LastAuthentication.AddMinutes(15).CompareTo(DateTime.Now) < 0)
                {
                    var authenticationProperties = new AuthenticationProperties();
                    authenticationProperties.Items["prompt"] = "login";
                    authenticationProperties.Items["login_hint"] = currentUser.Username;
                    authenticationProperties.RedirectUri = "/Internal/Account/SetPassword";
                    return Challenge(authenticationProperties, AzureADDefaults.AuthenticationScheme);
                } else if (currentUser.Existence == Existence.Hybrid && currentUser.LastAuthentication.AddMinutes(15).CompareTo(DateTime.Now) >= 0)
                {
                    return View();
                }
                else
                    return StatusCode(403);
            }
            else
            {
                List<NotificationToken> notificationTokens = await _context.NotificationTokens.ToListAsync();
                foreach (var Rtoken in notificationTokens)
                {
                    if (Rtoken.Token.Equals(token))
                    {
                        SetPasswordModel model = new SetPasswordModel
                        {
                            Token = token
                        };
                        return View(model);
                    }
                }
                return StatusCode(403);
            }
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SetPassword([Bind("Token", "NewPassword", "ConfirmPassword")]SetPasswordModel NewCredentials)
        {
            if (NewCredentials.Token == null)
                return StatusCode(500);
            else
            {
                List<NotificationToken> notificationTokens = await _context.NotificationTokens.ToListAsync();
                foreach (var Rtoken in notificationTokens)
                {
                    if (Rtoken.Token.Equals(NewCredentials.Token))
                    {
                        User identity = await _context.Users.FindAsync(Rtoken.LinkedUser.Username);
                        identity.Password = Password.HashPassword(NewCredentials.ConfirmPassword, Password.GetRandomSalt());
                        identity.LastPasswordChange = DateTime.Now;
                        _context.Users.Update(identity);
                        Rtoken.Vaild = false;
                        _context.NotificationTokens.Update(Rtoken);
                        await _context.SaveChangesAsync();
                        TempData["Field"] = "Password";
                        return RedirectToAction("SetSuccessful");
                    }
                }
                return StatusCode(500);
            }
        }

        public IActionResult SetSucessful()
        {
            return View();
        }

        public async Task<IActionResult> ChangePassword()
        {
            User identity = await _context.Users.FindAsync(HttpContext.User.FindFirstValue("preferred_username"));
            if (identity == null)
                return StatusCode(403);
            else if (identity.Existence == Existence.External)
                return StatusCode(403);
            else
                return View();
                
        }
        [HttpPost]
        public async Task<IActionResult> ChangePassword([Bind("CurrentPassword", "NewPassword", "ConfirmPassword")]ChangePasswordModel NewCredentials)
        {
            User identity = await _context.Users.FindAsync(HttpContext.User.FindFirstValue("preferred_username"));
            if (identity.Existence == Existence.External)
                return StatusCode(500);
            else
            {
                identity.Password = Password.HashPassword(NewCredentials.ConfirmPassword, Password.GetRandomSalt());
                identity.LastPasswordChange = DateTime.Now;
                _context.Users.Update(identity);
                await _context.SaveChangesAsync();
                TempData["Field"] = "Password";
                return RedirectToAction("SetSuccessful");
            }
        }
    }
}