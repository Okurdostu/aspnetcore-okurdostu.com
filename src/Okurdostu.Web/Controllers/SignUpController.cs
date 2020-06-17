﻿using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Okurdostu.Data.Model;
using Okurdostu.Web.Base;
using Okurdostu.Web.Extensions;
using Okurdostu.Web.Models;

namespace Okurdostu.Web.Controllers
{
    public class SignUpController : OkurdostuContextController
    {
        [Route("~/Kaydol")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Route("~/Kaydol")]
        public async Task<IActionResult> Index(SignUpModel Model)
        {
            if (ModelState.IsValid)
            {
                var User = new User
                {
                    Username = Model.Username,
                    Email = Model.Email,
                    Password = Model.Password.SHA512(),
                    FullName = Model.FullName,
                };
                try
                {
                    await Context.User.AddAsync(User);
                    var result = await Context.SaveChangesAsync();
                    if (result > 0)
                    {
                        var ClaimList = new List<Claim>();
                        ClaimList.Add(new Claim("Id", User.Id.ToString()));
                        ClaimList.Add(new Claim("Username", User.Username));
                        ClaimList.Add(new Claim("Email", User.Email));
                        ClaimList.Add(new Claim("FullName", User.FullName));
                        var ClaimsIdentity = new ClaimsIdentity(ClaimList, CookieAuthenticationDefaults.AuthenticationScheme);
                        var AuthProperties = new AuthenticationProperties
                        {
                            AllowRefresh = true
                        };
                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(ClaimsIdentity),
                            AuthProperties);

                        return Redirect("/beta");
                    }
                    else
                        TempData["SignUpMessage"] = "Başaramadık ve ne olduğunu bilmiyoruz";
                }
                catch (Exception e)
                {
                    if (e.InnerException.Message.Contains("Unique_Key_Username"))
                        TempData["SignUpMessage"] = "Bu kullanıcı adını kullanamazsınız";
                    else if (e.InnerException.Message.Contains("Unique_Key_Email"))
                        TempData["SignUpMessage"] = "Bu e-mail adresini kullanamazsınız";
                    else
                        TempData["SignUpMessage"] = "Başaramadık ve ne olduğunu bilmiyoruz"; //ex.innerex.message db log
                }
            }
            else
                TempData["SignUpMessage"] = "Verdiğiniz bilgilerin doğruluğunu kontrol edin";

            return View();
        }
    }
}
