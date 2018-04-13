﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RentalManagement.CustomFilters;

namespace RentalManagement.Controllers
{
    [AuthLog(Roles = "Tenant")]
    public class OrderDetailsController : Controller
    {
        // GET: OrderDetails
        public ActionResult Index()
        {
            ViewBag.Message = "Order Details Page.";

            return View();
        }
    }
}