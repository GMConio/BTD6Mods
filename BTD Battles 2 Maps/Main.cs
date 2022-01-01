﻿using MelonLoader;
using HarmonyLib;
using Assets.Scripts.Unity.UI_New.InGame;
using Assets.Scripts.Unity;
using System.IO;
using Assets.Main.Scenes;
using UnityEngine;
using System.Linq;
using BTD_Mod_Helper.Extensions;
using Assets.Scripts.Data.MapSets;
using Assets.Scripts.Models.Map.Spawners;
using Assets.Scripts.Models.Map;
using UnhollowerBaseLib;
using Assets.Scripts.Data;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper;
using Assets.Scripts.Unity.Map;
using Assets.Scripts.Unity.Bridge;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Net;
using Il2CppSystem.Collections.Generic;
using Assets.Scripts.Utils;
using BTD_Mod_Helper.Api.ModOptions;
using Il2CppSystem.Reflection;
using Assets.Scripts.Unity.UI_New.Main.MapSelect;
using Assets.Scripts.Unity.Player;
using NinjaKiwi.Common;

namespace BTDBattles2Maps
{
    public class Main : BloonsMod
    {
        public override string MelonInfoCsURL => "info";
        public override string LatestURL => "info";

        private bool First = true;

        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
        }
        static string LastMap = null;

        static bool isCustom(string map)
        {
            return mapList.Where(x => x.name == map).Count() > 0;
        }

        static MapInfo[] mapList = new MapInfo[]
        {
            new MapInfo("CastleRuins", MapDifficulty.Beginner, Maps.CastleRuins.pathmodel(), Maps.CastleRuins.spawner(), Maps.CastleRuins.areas(), "MusicDarkA", "Castle Ruins"),
            new MapInfo("Docks", MapDifficulty.Beginner, Maps.Docks.pathmodel(), Maps.Docks.spawner(), Maps.Docks.areas(), "MusicDarkA", "Docks"),
            new MapInfo("BloontoniumMines", MapDifficulty.Beginner, Maps.BloontoniumMines.pathmodel(), Maps.BloontoniumMines.spawner(), Maps.BloontoniumMines.areas(), "MusicDarkA", "Bloontonium Mines"),
            new MapInfo("InTheWall", MapDifficulty.Beginner, Maps.InTheWall.pathmodel(), Maps.InTheWall.spawner(), Maps.InTheWall.areas(), "MusicDarkA", "InTheWall")
        };

        [HarmonyPatch(typeof(TitleScreen), "Start")]
        public class Awake_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                foreach (var mapdata in mapList)
                {
                    GameData._instance.mapSet.Maps.items = GameData._instance.mapSet.Maps.items.AddTo(new MapDetails
                    {
                        id = mapdata.name,
                        isBrowserOnly = false,
                        isDebug = false,
                        difficulty = mapdata.difficulty,
                        unlockDifficulty = MapDifficulty.Beginner,
                        mapMusic = mapdata.mapMusic,
                        mapSprite = ModContent.GetSpriteReference<Main>(mapdata.name),
                        coopMapDivisionType = CoopDivision.FREE_FOR_ALL,
                    }).ToArray();

                    if (!LocalizationManager.Instance.textTable.ContainsKey(mapdata.name))
                    {
                        LocalizationManager.Instance.textTable.Add(mapdata.name, mapdata.mapDisplayName);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MapLoader), nameof(MapLoader.Load))]
        public class LoadMap
        {
            [HarmonyPrefix]
            internal static bool Fix(ref MapLoader __instance, ref string map, ref CoopDivision coopDivisionType, ref Il2CppSystem.Action<MapModel> loadedCallback)
            {
                LastMap = map;
                if (isCustom(LastMap))
                {
                    map = "MuddyPuddles";
                }
                return true;
            }

        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            bool inAGame = InGame.instance != null && InGame.instance.bridge != null;

            if (First && inAGame)
            {                
                foreach (var mapData in mapList)
                {                      
                    if (!Game.instance.GetBtd6Player().IsMapUnlocked(mapData.name))
                    {                       
                        Game.instance.GetBtd6Player().UnlockMap(mapData.name);
                        InGame.instance.Player.UnlockMap(mapData.name);
                    }
                }
                First = false;
            }
        }

        [HarmonyPatch(typeof(UnityToSimulation), nameof(UnityToSimulation.InitMap))]
        internal class InitMap_Patch
        {
            [HarmonyPrefix]
            internal static bool Prefix(UnityToSimulation __instance, ref MapModel map)
            {
                if (!isCustom(LastMap))
                {
                    return true;
                }

                MapInfo mapdata = mapList.Where(x => x.name == LastMap).First();
                Texture2D tex = ModContent.GetTexture<Main>(mapdata.name);
                byte[] filedata = null;

                filedata = Image.Resize(ImageConversion.EncodeToPNG(tex), 1750, 1064);
                float divx = 2;
                float divy = 1.21f;
                int marginx = 450;
                int marginy = 890;
                Bitmap old = new Bitmap(System.Drawing.Image.FromStream(new MemoryStream(filedata)));
                Bitmap newImage = new Bitmap(old.Width + marginx, old.Height + marginy);

                using (var graphics = System.Drawing.Graphics.FromImage(newImage))
                {
                    int x = (int)((newImage.Width - old.Width) / divx);
                    int y = (int)((newImage.Height - old.Height) / divy);
                    graphics.DrawImage(old, x, y);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        newImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        filedata = ms.ToArray();
                    }
                }

                ImageConversion.LoadImage(tex, filedata);
                var ob2 = GameObject.Find("MuddyPuddlesTerrain");
                ob2.GetComponent<Renderer>().material.mainTexture = tex;

                foreach (var ob in Object.FindObjectsOfType<GameObject>())
                {
                    if (ob.name.Contains("Festive") || ob.name.Contains("Rocket") || ob.name.Contains("Firework") || ob.name.Contains("Box") || ob.name.Contains("Candy") || ob.name.Contains("Gift") || ob.name.Contains("Snow") || ob.name.Contains("Ripples") || ob.name.Contains("Grass") || ob.name.Contains("Christmas") || ob.name.Contains("WhiteFlower") || ob.name.Contains("Tree") || ob.name.Contains("Rock") || ob.name.Contains("Shadow") || ob.name.Contains("WaterSplashes"))
                    {
                        if (ob.name != "MuddyPuddlesTerrain")
                        {
                            ob.transform.position = new Vector3(1000, 1000, 1000);
                        }
                    }
                }

                map.areas = mapdata.areas;
                map.spawner = mapdata.spawner;
                map.paths = mapdata.paths;
                map.name = mapdata.name;
                map.mapName = mapdata.name;

                if (GameObject.Find("Rain"))
                {
                    GameObject.Find("Rain").active = false;
                }

                return true;
            }
        }
    }
}