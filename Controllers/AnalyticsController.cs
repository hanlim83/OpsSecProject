﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpsSecProject.ViewModels;

namespace OpsSecProject.Controllers
{
    public class AnalyticsController : Controller
    {
        public async Task<IActionResult> Web()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Search(string query)
        {
            return View();
        }

        //Imported methods 

        public IActionResult Bar()
        {
            Random rnd = new Random();

            //list of department  
            var lstModel = new List<ChartsViewModel>();
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Technology",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Sales",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Marketing",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Human Resource",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Research and Development",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Acconting",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Support",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Logistics",
                Quantity = rnd.Next(10)
            });

            return View(lstModel);

        }

        public IActionResult Line()
        {
            Random rnd = new Random();

            //list of countries  
            var lstModel = new List<ChartsViewModel>();
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Brazil",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "USA",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "Ier",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "gfg",
                Quantity = rnd.Next(10)
            });
            lstModel.Add(new ChartsViewModel
            {
                DimensionOne = "ghA",
                Quantity = rnd.Next(10)
            });

            return View(lstModel);
        }
    }
}