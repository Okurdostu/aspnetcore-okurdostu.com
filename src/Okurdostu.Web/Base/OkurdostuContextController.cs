﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Okurdostu.Data;
using Okurdostu.Data.Model;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Okurdostu.Web.Base
{
    public class OkurdostuContextController<T> : Controller where T : OkurdostuContextController<T>
    {
        private ILogger<T> _logger;

        protected ILogger<T> Logger => _logger ?? (_logger = HttpContext.RequestServices.GetService<ILogger<T>>());


        private OkurdostuContext _con;
        public OkurdostuContext Context => _con ?? (OkurdostuContext)HttpContext?.RequestServices.GetService(typeof(OkurdostuContext));

       

        //başka bir controllera alınacak
        public async Task<User> GetAuthenticatedUserFromDatabaseAsync()
        {
            var Id = User.Identity.GetUserId();
            if (Id == null)
                return null;

            var _User = await Context.User.FirstOrDefaultAsync(x => x.Id == long.Parse(Id) && x.IsActive);
            return _User != null ? _User : null;
        }
    }
}
