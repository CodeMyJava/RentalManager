﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using RentalManagement.Models;
using RentalManagement.CustomFilters;
using Microsoft.AspNet.Identity;

namespace RentalManagement.Controllers
{
    [AuthLog(Roles = "Admin, Manager, Staff, Tenant")]
    public class MaintenanceRequestsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: MaintenanceRequests
        public ActionResult Index()
        {
            var assetGroupQuery = db.MaintenanceRequest.Include("Asset").GroupBy(a => a.Asset);
            Dictionary<string, string> numRequestPerAsset = new Dictionary<string, string>();
            foreach (var line in assetGroupQuery
                        .Include("Asset")
                        .Select(group => new {
                            group.Key.Name,
                            Count = group.Count().ToString()
                        })
                        .OrderByDescending(x => x.Name))
            {
                numRequestPerAsset.Add(line.Name, line.Count);
                Console.WriteLine("Asset name: {0}, requests: {1}", line.Name, line.Count);
            }
            ViewBag.NumRequestPerAsset = numRequestPerAsset;
            return View(db.MaintenanceRequest.OrderByDescending(a => a.CreatedDate).ToList());
        }

        // GET: MaintenanceRequests/Details/5
        public ActionResult Details(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            MaintenanceRequest maintenanceRequest = db.MaintenanceRequest.Find(id);
            if (maintenanceRequest == null)
            {
                return HttpNotFound();
            }
            return View(maintenanceRequest);
        }

        // GET: MaintenanceRequests/Create
        public ActionResult Create()
        {
            var assets = db.Assets.ToList();
            List<SelectListItem> list = assets.ConvertAll(a =>
                                        {
                                            return new SelectListItem()
                                            {
                                                Text = a.Name,
                                                Value = a.ID.ToString(),
                                                Selected = false
                                            };
                                        });
            ViewBag.AssetList = new SelectList(list, "Value", "Text");
            ViewBag.ApplianceList = null;

            if (User.IsInRole("Tenant"))
            {
                // Find the asset for the tenant   
                var currentUserId = User.Identity.GetUserId();
                var currentUser = db.Users.Include("Tenant").SingleOrDefault(s => s.Id == currentUserId);
                Tenant tenant = currentUser.Tenant;

                Asset asset = db.Occupancies
                            .Include("AssetID")
                            .Include("ClientID")
                            .Where(s => s.ClientID.ID == tenant.ID)
                            .First().AssetID;
                
                // Get list of appliances for the asset
                var appliancesInAsset = db.Assets.Include("Appliances").Where(a => a.ID == asset.ID).FirstOrDefault().Appliances;
                // 
                List<SelectListItem> aList = new List<SelectListItem>();
                foreach (Appliance a in appliancesInAsset)
                {
                    aList.Add(new SelectListItem()
                    {
                        Text = a.Name,
                        Value = a.ID.ToString(),
                        Selected = false
                    });
                }
                ViewBag.ApplianceList = new MultiSelectList(aList, "Value", "Text");
            }
            return View();
        }

        // POST: MaintenanceRequests/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthLog(Roles = "Admin, Manager, Staff, Tenant")]
        public ActionResult Create([Bind(Include = "Asset,Subject,RequestDetail")] MaintenanceRequest maintenanceRequest)
        {
            if (ModelState.IsValid)
            {
                // Tenant request creation
                if (User.IsInRole("Tenant"))
                {
                    maintenanceRequest.ID = Guid.NewGuid();
                    maintenanceRequest.CreatedDate = System.DateTime.Now;
                    maintenanceRequest.CompletedDate = null;

                    // Bind Asset and Tenant data from Occupancy for the currently logged in tenant user    
                    var currentUserId = User.Identity.GetUserId();
                    var currentUser = db.Users.Include("Tenant").SingleOrDefault(s => s.Id == currentUserId);
                    Tenant tenant = currentUser.Tenant;

                    maintenanceRequest.Tenant = tenant;
                        
                    Asset asset = db.Occupancies
                                .Include("AssetID")
                                .Include("ClientID")
                                .Where(s => s.ClientID.ID == tenant.ID)
                                .First().AssetID;
                    maintenanceRequest.Asset = asset;
                    db.MaintenanceRequest.Add(maintenanceRequest);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }

                // Admin, manager, staff request creation
                if (User.IsInRole("Admin") ||
                    User.IsInRole("Manager") ||
                    User.IsInRole("Staff"))
                {
                    maintenanceRequest.ID = Guid.NewGuid();
                    maintenanceRequest.CreatedDate = System.DateTime.Now;
                    maintenanceRequest.CompletedDate = null;

                    Asset selectedAsset = db.Assets.Find(Guid.Parse(Request["selectAsset"]));
                    maintenanceRequest.Asset = selectedAsset;
                    
                    // Look in Occupancies to check if a Tenant exists for the selected Asset
                    if (selectedAsset != null)
                    {
                        maintenanceRequest.Tenant = db.Occupancies
                                .Include("AssetID")
                                .Include("ClientID")
                                .Where(s => s.AssetID.ID == selectedAsset.ID)
                                .FirstOrDefault()?.ClientID;
                    }

                    db.MaintenanceRequest.Add(maintenanceRequest);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
            }

            return View(maintenanceRequest);
        }

        // GET: MaintenanceRequests/Edit/5
        public ActionResult Edit(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            MaintenanceRequest maintenanceRequest = db.MaintenanceRequest.Find(id);
            if (maintenanceRequest == null)
            {
                return HttpNotFound();
            }
            return View(maintenanceRequest);
        }

        // POST: MaintenanceRequests/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,CreatedDate,CompletedDate,Subject,RequestDetail,StatusDetail,FixDetail,HoursSpent")] MaintenanceRequest maintenanceRequest, string saveBtn, string closeBtn)
        {
            if (ModelState.IsValid && saveBtn != null)
            {
                db.Entry(maintenanceRequest).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SavedChangesMessage"] = "All Changes Have Been Saved!";
                return RedirectToAction("Edit");
            }

            if(ModelState.IsValid && closeBtn != null)
            {
                //alert user to enter completion date and display error msg
                do
                {
                    TempData["CloseRequestMsgAlert"] = "Please Enter a Completion Date to Close Request";

                } while (ViewData.ModelState["CompletedDate"] == null);
                return RedirectToAction("Edit");
            }
            // if completedDate is filled, move request to 'closed' table
            if (ViewData.ModelState["CompletedDate"] != null && ModelState.IsValid && closeBtn != null)
            {
                db.Entry(maintenanceRequest).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(maintenanceRequest);
        }

        // GET: MaintenanceRequests/Delete/5
        public ActionResult Delete(Guid? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            MaintenanceRequest maintenanceRequest = db.MaintenanceRequest.Find(id);
            if (maintenanceRequest == null)
            {
                return HttpNotFound();
            }
            return View(maintenanceRequest);
        }

        // POST: MaintenanceRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(Guid id)
        {
            MaintenanceRequest maintenanceRequest = db.MaintenanceRequest.Find(id);
            db.MaintenanceRequest.Remove(maintenanceRequest);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
