﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OneEchan.Backend.Models
{
    public class CheckContext : DbContext
    {
        public DbSet<WeiboModel> WeiboList { get; set; }
        public DbSet<CheckModel> CheckList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=./check.db");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CheckModel>().HasKey(x => new { x.ID, x.ItemID, x.SetName });
            modelBuilder.Entity<WeiboModel>().HasKey(x => new { x.ID, x.ItemID, x.SetName });
        }
    }
}
