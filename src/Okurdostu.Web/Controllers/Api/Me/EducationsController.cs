﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Okurdostu.Data;
using Okurdostu.Web.Base;
using Okurdostu.Web.Extensions;
using Okurdostu.Web.Filters;
using Okurdostu.Web.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Okurdostu.Web.Controllers.Api.Me
{
    [Route("api/me/educations")]
    public class EducationsController : SecureApiController
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly IHostingEnvironment Environment;
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
        public EducationsController(IHostingEnvironment env) => Environment = env;
#pragma warning restore CS0618 // Type or member is obsolete

        [NonAction]
        public bool DeleteFileFromServer(string filePathAfterRootPath)
        {
            if (System.IO.File.Exists(Environment.WebRootPath + filePathAfterRootPath))
            {
                System.IO.File.Delete(Environment.WebRootPath + filePathAfterRootPath);
                return true;
            }
            else
            {
                return false;
            }
        }

        // /api/me/educations/{Id} : get single education
        [HttpGet("{Id}")]
        public async Task<IActionResult> GetSingle(Guid Id)
        {
            ReturnModel rm = new ReturnModel();
            var AuthenticatedUserId = User.Identity.GetUserId();
            var Education = await Context.UserEducation.FirstOrDefaultAsync(x => x.Id == Id && !x.IsRemoved && x.UserId == Guid.Parse(AuthenticatedUserId));

            if (Education != null)
            {
                var educationModel = new EducationModel
                {
                    UniversityId = Education.UniversityId,
                    Department = Education.Department,
                    EducationId = Id,
                    ActivitiesSocieties = Education.ActivitiesSocieties,
                    Startyear = int.Parse(Education.StartYear),
                    Finishyear = int.Parse(Education.EndYear),

                };
                educationModel.AreUniversityorDepartmentCanEditable = Education.AreUniversityorDepartmentCanEditable();
                if (educationModel.ActivitiesSocieties == null || educationModel.ActivitiesSocieties == "undefined")
                {
                    educationModel.ActivitiesSocieties = "";
                }

                rm.Data = educationModel;
            }
            else
            {
                return Error(rm);
            }

            return Succes(rm);
        }

        // /api/me/educations : get all educations
        [HttpGet("")]
        public async Task<IActionResult> GetList()
        {
            ReturnModel rm = new ReturnModel();

            var Educations = await Context.UserEducation.Where(x => x.UserId == Guid.Parse(User.Identity.GetUserId())).Select(x => new
            {
                x.Id,
                x.EndYear,
                x.IsRemoved,
                x.StartYear,
                x.IsConfirmed,
                x.IsActiveEducation,
                x.IsSentToConfirmation,

                universityPageUrl = "/universite/" + x.University.FriendlyName,
                universityName = x.University.Name,
                universityLogoUrl = x.University.LogoUrl,
                universityId = x.UniversityId,
            }).ToListAsync();

            if (Educations != null)
            {
                rm.Data = Educations;
                return Succes(rm);
            }

            rm.Code = 404;
            return Error(rm);
        }

        public async Task<bool> IsCanRemovable(UserEducation edu)
        {
            if (edu.IsConfirmed || edu.IsActiveEducation)
            {
                var EducationUserUnRemovedNeed = await Context.Need.AnyAsync(x => !x.IsRemoved && x.UserId == edu.UserId);
                return !EducationUserUnRemovedNeed;
            }
            else
            {
                return true;
            }
        }

        // /api/me/educations/{Id} -- isRemoved = true
        [HttpPatch("{Id}")]
        public async Task<IActionResult> PatchRemove(Guid Id)
        {
            var AuthenticatedUserId = User.Identity.GetUserId();
            ReturnModel rm = new ReturnModel();

            if (!ModelState.IsValid)
            {
                rm.Message = "Silinmesi gereken eğitim bilgisine ulaşılamadı";
                rm.InternalMessage = "Id is required";
                return Error(rm);
            }

            var deletedEducation = await Context.UserEducation.FirstOrDefaultAsync(x => x.Id == Id && !x.IsRemoved && Guid.Parse(AuthenticatedUserId) == x.UserId);

            if (deletedEducation != null)
            {
                if (await IsCanRemovable(deletedEducation).ConfigureAwait(false))
                {
                    deletedEducation.IsRemoved = true;
                    var result = await Context.SaveChangesAsync();

                    if (result > 0)
                    {
                        rm.Message = "Eğitim bilgisi kaldırıldı";
                        if (deletedEducation.IsSentToConfirmation)
                        {
                            var educationDocuments = await Context.UserEducationDoc.Where(x => x.UserEducationId == deletedEducation.Id).ToListAsync();
                            foreach (var item in educationDocuments)
                            {
                                if (DeleteFileFromServer(item.PathAfterRoot))
                                {
                                    Context.Remove(item);
                                }
                            }
                            await Context.SaveChangesAsync();
                        }
                        return Succes(rm);
                    }
                    else
                    {
                        rm.Message = "Başaramadık, ne olduğunu bilmiyoruz";
                        rm.InternalMessage = "Changes aren't save";
                        return Error(rm);
                    }
                }
                else
                {
                    rm.Message = "Bu eğitimi silemezsiniz";

                    TempData["ProfileMessage"] = "İhtiyaç kampanyanız olduğu için" +
                        "<br />" +
                        "Aktif olan eğitim bilginizi silemezsiniz." +
                        "<br />" +
                        "Aktif olan eğitim bilgisi, belge yollayarak hala burada okuduğunuzu iddia ettiğiniz bir eğitim bilgisidir." +
                        "<br/>" +
                        "Daha fazla ayrıntı ve işlem için: info@okurdostu.com";

                    return Error(rm);
                }
            }
            else
            {
                rm.Message = "Böyle bir eğitiminiz yok";
                rm.InternalMessage = "Education is null";
                return Error(rm);
            }
        }

        // /api/me/educations/{Id} -- edit all columns
        [HttpPut("{Id}")]
        public async Task<IActionResult> PutEdit(Guid Id, EducationModel Model)
        {
            var AuthenticatedUserId = User.Identity.GetUserId();
            ReturnModel rm = new ReturnModel();

            var editedEducation = await Context.UserEducation.FirstOrDefaultAsync(x => x.Id == Id && !x.IsRemoved && Guid.Parse(AuthenticatedUserId) == x.UserId);

            if (editedEducation != null)
            {
                editedEducation.StartYear = Model.Startyear.ToString();
                editedEducation.EndYear = Model.Finishyear.ToString();
                editedEducation.ActivitiesSocieties = Model.ActivitiesSocieties;

                if (editedEducation.AreUniversityorDepartmentCanEditable())
                {
                    editedEducation.UniversityId = Model.UniversityId;
                    editedEducation.Department = Model.Department;
                }
            }
            else
            {
                rm.Message = "Böyle bir eğitiminiz yok";
                rm.InternalMessage = "Education is null";

                return Error(rm);
            }

            try
            {
                var result = await Context.SaveChangesAsync();
                if (result > 0)
                {
                    rm.Message = "Eğitim bilgisi kaydedildi";
                    return Succes(rm);
                }
                else
                {
                    rm.Message = "Bir işlem yapılmadı";
                    return Error(rm);
                }
            }
            catch (Exception e)
            {
                string innerMessage = (e.InnerException != null) ? e.InnerException.Message.ToLower() : "";

                if (innerMessage.Contains("department"))
                {
                    rm.Message = "Bölüm bilgilerine ulaşamadık veya eksik";
                }
                else if (innerMessage.Contains("university"))
                {
                    rm.Message = "Üniversite bilgilerine ulaşamadık veya eksik";
                }
                else if (innerMessage.Contains("startyear") || innerMessage.Contains("endyear"))
                {
                    rm.Message = "Başlangıç veya bitiş yılını kontrol edin";
                }
                else
                {
                    rm.Message = "Başaramadık ve ne olduğunu bilmiyoruz, tekrar deneyin";
                }

                return Error(rm);
            }
        }

        // /api/me/educations -- add a new education
        [ServiceFilter(typeof(ConfirmedEmailFilter))]
        [HttpPost("")]
        public async Task<IActionResult> PostAdd(EducationModel Model)
        {
            var AuthenticatedUserId = User.Identity.GetUserId();
            ReturnModel rm = new ReturnModel();

            if (Model.Startyear > Model.Finishyear)
            {
                rm.Message = "Başlangıç yılı, bitiş yılından büyük olamaz";
                rm.Code = 200;
                return Error(rm);
            }
            else if (Model.Startyear < 1980 || Model.Startyear > DateTime.Now.Year || Model.Finishyear < 1980 || Model.Startyear > DateTime.Now.Year + 7)
            {
                rm.Message = "Başlangıç yılı, bitiş yılı ile alakalı bilgileri kontrol edip, tekrar deneyin";
                rm.Code = 200;
                return Error(rm);
            }

            var NewEducation = new UserEducation
            {
                UserId = Guid.Parse(AuthenticatedUserId),
                UniversityId = Model.UniversityId,
                Department = Model.Department,
                ActivitiesSocieties = Model.ActivitiesSocieties,
                StartYear = Model.Startyear.ToString(),
                EndYear = Model.Finishyear.ToString(),
            };
            await Context.AddAsync(NewEducation);
            try
            {
                var result = await Context.SaveChangesAsync();
                if (result > 0)
                {
                    rm.Message = "Eğitim bilgisi kaydedildi";
                    return Succes(rm);
                }
                else
                {
                    rm.Message = "Bir işlem yapılmadı";
                    rm.Code = 1001;
                    return Error(rm);
                }
            }
            catch (Exception e)
            {
                rm.Code = 200;

                string innerMessage = (e.InnerException != null) ? e.InnerException.Message.ToLower() : "";

                if (innerMessage.Contains("department"))
                {
                    rm.Message = "Bölüm bilgilerine ulaşamadık veya eksik";
                }
                else if (innerMessage.Contains("university"))
                {
                    rm.Message = "Üniversite bilgilerine ulaşamadık veya eksik";
                }
                else if (innerMessage.Contains("startyear") || innerMessage.Contains("endyear"))
                {
                    rm.Message = "Başlangıç veya bitiş yılını kontrol edin";
                }
                else
                {
                    rm.Message = "Başaramadık ve ne olduğunu bilmiyoruz, tekrar deneyin";
                }

                return Error(rm);
            }
        }
    }
}