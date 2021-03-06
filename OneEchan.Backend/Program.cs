﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using OneEchan.Backend.Models;
using OneEchan.Backend.QCloud.Api;
using OneEchan.Core.Models;

namespace OneEchan.Backend
{
    public class Program
    {
        private static VideoCloud Cloud { get; set; } 
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
        private static string DownloadFolder => Configuration[nameof(DownloadFolder)];
        private static int AppID => int.Parse(Configuration[nameof(AppID)]);
        private static string SecretID => Configuration[nameof(SecretID)];
        private static string SecretKey => Configuration[nameof(SecretKey)];
        private static string AccessToken => Configuration[nameof(AccessToken)];
        private static string RegexPattern => Configuration[nameof(RegexPattern)];
        private static string BucketName => Configuration[nameof(BucketName)];
        private static bool ShareToWeibo => bool.Parse(Configuration[nameof(ShareToWeibo)]);
        private static string EnSite => Configuration[nameof(EnSite)];
        private static string ZhSite => Configuration[nameof(ZhSite)];
        private static string RuSite => Configuration[nameof(RuSite)];
        private static bool EnableAdvLogging => bool.Parse(Configuration[nameof(EnableAdvLogging)]);
        private static int Interval => int.Parse(Configuration[nameof(Interval)]);
        private static IConfigurationRoot Configuration { get; set; }

        public static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables();
            Configuration = builder.Build();
            Task.Run(async () =>
            {
                Cloud = new VideoCloud(AppID, SecretID, SecretKey);
                OpenWeen.Core.Api.Entity.AccessToken = AccessToken;
                while (true)
                {
                    await CheckQuality();
                    if (ShareToWeibo)
                        await CheckWeiboShare();
                    await UploadTask();
                    if (EnableAdvLogging)
                        Logger.Info("Waiting...");
                    await Task.Delay(TimeSpan.FromMinutes(Interval));
                }
            });
            Console.WriteLine("Running...");
            do
            {
                while (!Console.KeyAvailable)
                {
                    switch (Console.ReadLine())
                    {
                        case "status":
                            using (var ctx = new CheckContext())
                            {
                                Console.WriteLine($"{nameof(ctx.CheckList)} {ctx.CheckList.Count()}");
                                foreach (var item in ctx.CheckList)
                                    Console.WriteLine(item);
                                Console.WriteLine($"{nameof(ctx.WeiboList)} {ctx.WeiboList.Count()}");
                                foreach (var item in ctx.WeiboList)
                                    Console.WriteLine(item);
                                //Console.WriteLine(File.ReadLines("log.txt").LastOrDefault());
                            }
                            break;
                        default:
                            break;
                    }
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
        }

        private static async Task CheckWeiboShare()
        {
            using (var ctx = new CheckContext())
            {
                if (EnableAdvLogging)
                    Logger.Info("checking for weibo share");
                for (int i = 0; i < ctx.WeiboList.Count(); i++)
                {
                    var item = ctx.WeiboList.ToList()[i];
                    if (EnableAdvLogging)
                        Logger.Info($"checking for {item} weibo");
                    dynamic obj = await AutoRetry(Cloud.GetFileStat(BucketName, $"/{item.Name}/{item.SetName}.mp4"));
                    if (!string.IsNullOrEmpty((string)obj.data?.video_cover))
                    {
                        if (EnableAdvLogging)
                            Logger.Info($"sending weibo {item}");
                        try
                        {
                            var url = $"http://OneEchan.moe/Watch?id={item.ItemID}&set={double.Parse(item.SetName)}";
                            //var shorturl = await OpenWeen.Core.Api.ShortUrl.Shorten(url);
                            using (var client = new HttpClient())
                            using (Stream stream = await client.GetStreamAsync((string)obj.data?.video_cover))
                            using (var memStream = new MemoryStream())
                            {
                                await stream.CopyToAsync(memStream);
                                await OpenWeen.Core.Api.Statuses.PostWeibo.PostWithPic($"{item.ZhTW} - {item.SetName} {url}", memStream.ToArray());
                            }
                            ctx.WeiboList.Remove(item);
                            ctx.SaveChanges();
                            if (EnableAdvLogging)
                                Logger.Info($"weibo {item} sended");
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"can not share to weibo {e.Message}");
                        }
                    }
                }
                if (EnableAdvLogging)
                    Logger.Info("checking for weibo is done");
            }
        }

        private static async Task CheckQuality()
        {
            using (var ctx = new CheckContext())
            {
                if (EnableAdvLogging)
                    Logger.Info("checking for quality");
                for (int i = 0; i < ctx.CheckList.Count(); i++)
                {
                    var item = ctx.CheckList.ToList()[i];
                    if (EnableAdvLogging)
                        Logger.Info($"checking for {item} quality");
                    if (CheckForVideoQuality(item.Name, item.SetName, await AutoRetry(Cloud.GetFileStat(BucketName, $"/{item.Name}/{item.SetName}.mp4"))))
                    {
                        ctx.CheckList.Remove(item);
                        ctx.SaveChanges();
                        if (EnableAdvLogging)
                            Logger.Info($"{item} quality is done, remove");
                    }
                }
                if (EnableAdvLogging)
                    Logger.Info("checking for quality is done");
            }
        }

        private static async Task UploadTask()
        {
            var files = Directory.GetFiles(DownloadFolder);
            foreach (var item in files)
            {
                if (Path.GetExtension(item) != ".mp4") continue;
                var match = Regex.Match(item, RegexPattern);
                if (!match.Success) continue;
                var title = GetTitle(match);
                var setName = GetSetName(match);
                if (EnableAdvLogging)
                    Logger.Info($"detecting file {title} {setName}");
                dynamic obj = await AutoRetry(Cloud.GetFolderStat(BucketName, $"/{title}/"));
                if (obj != null && obj.code != 0)
                {
                    Logger.Info($"create {title} folder");
                    await Cloud.CreateFolder(BucketName, $"/{title}/");
                }
                obj = null;
                obj = await AutoRetry(Cloud.GetFileStat(BucketName, $"/{title}/{setName}.mp4"));
                if (obj != null && obj.code != 0)
                {
                    Logger.Info($"uploading {title} {setName}...");
                    await Cloud.SliceUploadFile(BucketName, $"/{title}/{setName}.mp4", item);
                    await AddSet(item, title, setName);
                    Logger.Info($"upload {title} {setName} complete");
                }
                else if (obj.code == 0)
                {
                    await CheckForVideoFile(item, title, setName, obj);
                }
                if (EnableAdvLogging)
                    Logger.Info($"file {title} {setName} complete");
            }
        }

        private static async Task AddSet(string item, string title, string setName)
        {
            if (EnableAdvLogging)
                Logger.Info($"adding {title} {setName} into database");
            using (var context = new AnimateDatabaseContext())
            {
                if (!context.AnimateList.Any(anime => anime.EnUs == title))
                {
                    if (EnableAdvLogging)
                        Logger.Info("cannot find id, create new");
                    context.AnimateList.Add(new AnimateList { EnUs = title, Updated_At = DateTime.Now });
                    context.SaveChanges();
                }
                var id = context.AnimateList.FirstOrDefault(anime => anime.EnUs == title).Id;
                if (id != -1)
                {
                    if (EnableAdvLogging)
                        Logger.Info($"checking for file data...");
                    dynamic obj = await AutoRetry(Cloud.GetFileStat(BucketName, $"/{title}/{setName}.mp4"));
                    if (obj != null && obj.code == 0)
                    {
                        if (obj.data.access_url != null)
                        {
                            if (context.SetDetail.Any(anime=>anime.Id == id && anime.SetName == double.Parse(setName)))
                            {
                                if (EnableAdvLogging)
                                    Logger.Info("existed,add check");
                                var animeItem = context.AnimateList.FirstOrDefault(anime => anime.Id == id);
                                using (var ctx = new CheckContext())
                                {
                                    if (!ctx.CheckList.Any(check => check.ID == id && check.SetName == setName))
                                        ctx.CheckList.Add(new CheckModel { ItemID = id, Name = title, SetName = setName });
                                    if (ShareToWeibo && !ctx.WeiboList.Any(check => check.ID == id && check.SetName == setName))
                                        ctx.WeiboList.Add(new WeiboModel { ItemID = id, Name = title, SetName = setName, ZhTW = animeItem.ZhTw });
                                    ctx.SaveChanges();
                                }
                            }
                            else
                            {
                                if (EnableAdvLogging)
                                    Logger.Info("adding set...");
                                context.SetDetail.Add(new SetDetail { Id = id, SetName = double.Parse(setName), FilePath = obj.data.access_url, ClickCount = 0, Created_At = DateTime.Now });
                                var animeItem = context.AnimateList.FirstOrDefault(anime => anime.Id == id);
                                animeItem.Updated_At = DateTime.Now;
                                animeItem = await GetAnimeTitle(title, animeItem);
                                context.Entry(animeItem).State = EntityState.Modified;
                                context.SaveChanges();
                                using (var ctx = new CheckContext())
                                {
                                    if (ctx.CheckList.Count(check => check.ID == id && check.SetName == setName) == 0)
                                        ctx.CheckList.Add(new CheckModel { ItemID = id, Name = title, SetName = setName });
                                    if (ShareToWeibo && ctx.WeiboList.Count(check => check.ID == id && check.SetName == setName) == 0)
                                        ctx.WeiboList.Add(new WeiboModel { ItemID = id, Name = title, SetName = setName, ZhTW = animeItem.ZhTw });
                                    ctx.SaveChanges();
                                }
                                if (EnableAdvLogging)
                                    Logger.Info("add set complete");
                            }
                        }
                    }
                }
            }
        }

        private static async Task<AnimateList> GetAnimeTitle(string title, AnimateList animeItem)
        {
            try
            {
                if (string.IsNullOrEmpty(animeItem.JaJp) || string.IsNullOrEmpty(animeItem.RuRu) || string.IsNullOrEmpty(animeItem.ZhTw))
                {
                    if (EnableAdvLogging)
                        Logger.Info($"getting anime title for {title}");
                    animeItem.JaJp = await GetLName(title, EnSite, (node) =>
                    {
                        return new
                        {
                            Name = node.TextContent.Trim(),
                            LName = node.NextSibling.NextSibling.FirstChild.TextContent.Trim(),
                        };
                    });
                    animeItem.ZhTw = await GetLName(animeItem.JaJp, ZhSite, (node) =>
                    {
                        return new
                        {
                            LName = node.TextContent.Trim(),
                            Name = node.NextSibling.NextSibling.FirstChild.TextContent.Trim(),
                        };
                    });
                    animeItem.RuRu = await GetLName(animeItem.JaJp, RuSite, (node) =>
                    {
                        return new
                        {
                            LName = node.TextContent.Trim(),
                            Name = node.NextSibling.NextSibling.FirstChild.TextContent.Trim(),
                        };
                    });
                    if (EnableAdvLogging)
                        Logger.Info($"get anime name complete, {animeItem.JaJp} {animeItem.RuRu} {animeItem.ZhTw}");
                }
            }
            catch (Exception e)
            {
                Logger.Warn($"can not get JName {e.Message}");
            }
            return animeItem;
        }

        private static async Task<string> GetLName(string title, string link, Func<IElement, dynamic> gen)
        {
            string str = "";
            using (var client = new HttpClient())
            {
                str = await client.GetStringAsync(link);
            }
            var doc = new HtmlParser().Parse(str);

            var items = from element in doc.All
                        where element?.Attributes["class"]?.Value == "name"
                        select gen(element);
            if (items.ToList().FirstOrDefault(a => a.Name == title) != null)
            {
                return items.ToList().FirstOrDefault(a => a.Name == title).LName;
            }
            return null;
        }

        private static async Task CheckForVideoFile(string item, string title, string setName, dynamic obj)
        {
            var file = new FileInfo(item);
            if (obj != null && obj.data?.filelen != file.Length)
            {
                Logger.Warn($"file {title} {setName} reuploading");
                await Cloud.DeleteFile(BucketName, $"/{title}/{setName}.mp4");
                await Cloud.SliceUploadFile(BucketName, $"/{title}/{setName}.mp4", item);
                using (var context = new AnimateDatabaseContext())
                using (var ctx = new CheckContext())
                {
                    var id = context.AnimateList.FirstOrDefault(anime => anime.EnUs == title).Id;
                    if (id != -1)
                    {
                        if (ctx.CheckList.Count(check => check.ID == id && check.SetName == setName) == 0)
                            ctx.CheckList.Add(new CheckModel { ItemID = id, Name = title, SetName = setName });
                        if (ShareToWeibo && ctx.WeiboList.Count(check => check.ID == id && check.SetName == setName) == 0)
                            ctx.WeiboList.Add(new WeiboModel { ItemID = id, Name = title, SetName = setName, ZhTW = "" });
                        ctx.SaveChanges();
                    }
                }
                Logger.Info($"reupload {title} {setName} complete");
            }
        }

        private static bool CheckForVideoQuality(string title, string setName, dynamic obj)
        {
            using (var context = new AnimateDatabaseContext())
            {
                var id = context.AnimateList.FirstOrDefault(anime => anime.EnUs == title).Id;
                if (id != -1)
                {
                    var set = context.SetDetail.FirstOrDefault(anime => anime.Id == id && anime.SetName == double.Parse(setName));
                    if (set != null)
                    {
                        if (string.IsNullOrEmpty(set.FileThumb) && !string.IsNullOrEmpty((string)obj.data?.video_cover))
                        {
                            if (EnableAdvLogging)
                                Logger.Info("add file thumb");
                            set.FileThumb = obj.data.video_cover;
                        }
                        if (string.IsNullOrEmpty(set.LowQuality) && !string.IsNullOrEmpty((string)obj.data?.video_play_url?.f10))
                        {
                            if (EnableAdvLogging)
                                Logger.Info("add LowQuality");
                            set.LowQuality = obj.data.video_play_url.f10;
                        }
                        if (string.IsNullOrEmpty(set.MediumQuality) && !string.IsNullOrEmpty((string)obj.data?.video_play_url?.f20))
                        {
                            if (EnableAdvLogging)
                                Logger.Info("add MediumQuality");
                            set.MediumQuality = obj.data.video_play_url.f20;
                        }
                        if (string.IsNullOrEmpty(set.HighQuality) && !string.IsNullOrEmpty((string)obj.data?.video_play_url?.f30))
                        {
                            if (EnableAdvLogging)
                                Logger.Info("add HighQuality");
                            set.HighQuality = obj.data.video_play_url.f30;
                        }
                        if (string.IsNullOrEmpty(set.OriginalQuality) && !string.IsNullOrEmpty((string)obj.data?.video_play_url?.f0))
                        {
                            if (EnableAdvLogging)
                                Logger.Info("add OriginalQuality");
                            set.OriginalQuality = obj.data.video_play_url.f0;
                        }
                        context.Entry(set).State = EntityState.Modified;
                        context.SaveChanges();
                        if (!string.IsNullOrEmpty(set.FileThumb) && !string.IsNullOrEmpty(set.LowQuality) && !string.IsNullOrEmpty(set.MediumQuality) && !string.IsNullOrEmpty(set.HighQuality) && !string.IsNullOrEmpty(set.OriginalQuality))
                            return true;
                    }
                }
            }
            return false;
        }

        private static async Task<object> AutoRetry(Task<string> task)
        {
            var count = 0;
            while (count < 3)
            {
                try
                {
                    return JsonConvert.DeserializeObject(await task);
                }
                catch
                {
                    count++;
                    Logger.Error($"failed,retry");
                    continue;
                }
            }
            throw new Exception("failed");
        }

        private static string GetSetName(Match match) 
            => match.Groups[2].Value.Trim();

        private static string GetTitle(Match match)
            => match.Groups[1].Value.Trim();
    }
}
