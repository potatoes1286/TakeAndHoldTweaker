﻿using Deli.Newtonsoft.Json;
using FistVR;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using TNHTweaker.ObjectTemplates;
using TNHTweaker.Utilities;
using UnityEngine.Networking;
using Steamworks;

namespace TNHTweaker.Patches
{
    public static class HighScorePatches
    {

        private static string[] equipment = { "Spawnlock", "Limited" };
        private static string[] health = { "Standard", "One-Hit" };
        private static string[] length = { "5-Hold", "Endless", "3-Hold" };


        [HarmonyPatch(typeof(TNH_Manager), "DelayedInit")]
        [HarmonyPrefix]
        public static bool StartOfGamePatch(TNH_Manager __instance)
        {
            if (!__instance.m_hasInit && __instance.AIManager.HasInit)
            {
                //Clear all entries from the tracked stats
                TNHTweaker.HoldActions.Clear();
                TNHTweaker.HoldStats.Clear();

                TNHTweaker.GunsRecycled = 0;
                TNHTweaker.ShotsFired = 0;

                TNHTweakerLogger.Log("Delayed init", TNHTweakerLogger.LogType.General);
            }
            
            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "InitPlayerPosition")]
        [HarmonyPrefix]
        public static bool TrackPlayerSpawnPatch(TNH_Manager __instance)
        {
            TNHTweaker.HoldActions[0].Add($"Spawned At Supply {__instance.m_curPointSequence.StartSupplyPointIndex}");

            TNHTweakerLogger.Log("Spawned Player", TNHTweakerLogger.LogType.General);

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "HoldPointCompleted")]
        [HarmonyPrefix]
        public static bool TrackHoldCompletion(TNH_Manager __instance)
        {
            TNHTweakerLogger.Log("Hold Completion", TNHTweakerLogger.LogType.General);

            TNHTweaker.HoldStats.Add(new HoldStats()
            {
                SosigsKilled = __instance.Stats[3],
                MeleeKills = __instance.Stats[5],
                Headshots = __instance.Stats[4],
                TokensSpent = __instance.Stats[8],
                GunsRecycled = TNHTweaker.GunsRecycled,
                AmmoSpent = TNHTweaker.ShotsFired
            });

            __instance.Stats[3] = 0;
            __instance.Stats[5] = 0;
            __instance.Stats[4] = 0;
            __instance.Stats[8] = 0;
            TNHTweaker.GunsRecycled = 0;
            TNHTweaker.ShotsFired = 0;

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "SetLevel")]
        [HarmonyPrefix]
        public static bool TrackNextLevel(TNH_Manager __instance)
        {
            TNHTweakerLogger.Log("Set Level", TNHTweakerLogger.LogType.General);
            TNHTweaker.HoldActions.Add(new List<string>());

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Dead")]
        [HarmonyPrefix]
        public static bool TrackDeath(TNH_Manager __instance)
        {
            TNHTweakerLogger.Log("Died", TNHTweakerLogger.LogType.General);
            TNHTweaker.HoldActions.Last().Add("Died");

            TNHTweaker.HoldStats.Add(new HoldStats()
            {
                SosigsKilled = __instance.Stats[3],
                MeleeKills = __instance.Stats[5],
                Headshots = __instance.Stats[4],
                TokensSpent = __instance.Stats[8],
                GunsRecycled = TNHTweaker.GunsRecycled,
                AmmoSpent = TNHTweaker.ShotsFired
            });

            __instance.Stats[3] = 0;
            __instance.Stats[5] = 0;
            __instance.Stats[4] = 0;
            __instance.Stats[8] = 0;
            TNHTweaker.GunsRecycled = 0;
            TNHTweaker.ShotsFired = 0;

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "SetPhase_Completed")]
        [HarmonyPrefix]
        public static bool TrackVictory(TNH_Manager __instance)
        {
            TNHTweakerLogger.Log("Victory", TNHTweakerLogger.LogType.General);
            TNHTweaker.HoldActions.Last().Add("Victory");

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "DispatchScore")]
        [HarmonyPrefix]
        public static bool ResetStats(TNH_Manager __instance)
        {
            foreach(HoldStats stat in TNHTweaker.HoldStats)
            {
                __instance.Stats[3] += stat.SosigsKilled;
                __instance.Stats[5] += stat.MeleeKills;
                __instance.Stats[4] += stat.Headshots;
                __instance.Stats[8] += stat.TokensSpent;
            }

            return true;
        }


        [HarmonyPatch(typeof(TNH_Manager), "OnShotFired")]
        [HarmonyPrefix]
        public static bool TrackShotFired(TNH_Manager __instance)
        {
            TNHTweaker.ShotsFired += 1;

            return true;
        }


        [HarmonyPatch(typeof(TNH_HoldPoint), "BeginHoldChallenge")]
        [HarmonyPrefix]
        public static bool TrackHoldStart(TNH_HoldPoint __instance)
        {
            TNHTweakerLogger.Log("Hold Start", TNHTweakerLogger.LogType.General);
            TNHTweaker.HoldActions[__instance.M.m_level].Add($"Entered Hold {__instance.M.HoldPoints.IndexOf(__instance)}");

            return true;
        }


        [HarmonyPatch(typeof(TNH_GunRecycler), "Button_Recycler")]
        [HarmonyPrefix]
        public static bool TrackRecyclePatch(TNH_GunRecycler __instance)
        {
            TNHTweakerLogger.Log("Recycle button", TNHTweakerLogger.LogType.General);
            if (__instance.m_detectedFirearms.Count > 0 && __instance.m_detectedFirearms[0] != null)
            {
                TNHTweaker.HoldActions[__instance.M.m_level].Add($"Recycled {__instance.m_detectedFirearms[0].ObjectWrapper.DisplayName}");
                TNHTweaker.GunsRecycled += 1;
            }

            return true;
        }


        [HarmonyPatch(typeof(TNH_SupplyPoint), "TestVisited")]
        [HarmonyPrefix]
        public static bool TrackSupplyVisitsPatch(TNH_SupplyPoint __instance, ref bool __result)
        {
            if (!__instance.m_isconfigured)
            {
                __result = false;
                return false;
            }

            bool flag = __instance.TestVolumeBool(__instance.Bounds, GM.CurrentPlayerBody.transform.position);
            if (flag)
            {
                if (!__instance.m_hasBeenVisited && __instance.m_contact != null)
                {
                    __instance.m_contact.SetVisited(true);
                    TNHTweakerLogger.Log("Visiting supply", TNHTweakerLogger.LogType.General);
                    TNHTweaker.HoldActions[__instance.M.m_level].Add($"Entered Supply {__instance.M.SupplyPoints.IndexOf(__instance)}");
                }
                __instance.m_hasBeenVisited = true;
            }
            __result = flag;

            return false;
        }



        [HarmonyPatch(typeof(TNH_ScoreDisplay), "SubmitScoreAndGoToBoard")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool PreventScoring(TNH_ScoreDisplay __instance, int score)
        {

            GM.Omni.OmniFlags.AddScore(__instance.m_curSequenceID, score);

            __instance.m_hasCurrentScore = true;
            __instance.m_currentScore = score;

            AnvilManager.Instance.StartCoroutine(SendScore(score));

            __instance.RedrawHighScoreDisplay(__instance.m_curSequenceID);

            GM.Omni.SaveToFile();

            return false;
        }


        public static ScoreEntry GetScoreEntry(TNH_Manager instance, int score)
        {
            ScoreEntry entry = new ScoreEntry();

            entry.Name = SteamFriends.GetPersonaName();
            entry.Score = score;
            entry.Character = instance.C.DisplayName;
            entry.Map = instance.LevelName;
            entry.EquipmentMode = equipment[(int)GM.TNHOptions.EquipmentModeSetting];
            entry.HealthMode = health[(int)GM.TNHOptions.HealthModeSetting];
            entry.GameLength = length[(int)GM.TNHOptions.ProgressionTypeSetting];
            entry.HoldActions = JsonConvert.SerializeObject(TNHTweaker.HoldActions);
            entry.HoldStats = JsonConvert.SerializeObject(TNHTweaker.HoldStats);

            return entry;
        }

        private static List<Vector3IntSerializable> GetHoldList()
        {
            return GM.TNH_Manager.HoldPoints.Select(o => new Vector3IntSerializable(o.SpawnPoint_SystemNode.position)).ToList();
        }

        private static List<Vector3IntSerializable> GetSupplyList()
        {
            return GM.TNH_Manager.SupplyPoints.Select(o => new Vector3IntSerializable(o.SpawnPoint_PlayerSpawn.position)).ToList();
        }
        

        public static UnityWebRequest GetScoresAPIWebRequest(int score)
        {
            string url = "https://tnh-dashboard.azure-api.net/v1/api/scores";

            UnityWebRequest www = new UnityWebRequest(url, "Put");

            ScoreEntry entry = GetScoreEntry(GM.TNH_Manager, score);

            string data = JsonConvert.SerializeObject(entry);

            TNHTweakerLogger.Log("Sending score entry: \n" + data, TNHTweakerLogger.LogType.TNH);

            www.SetRequestHeader(Globals.Accept, "*/*");
            www.SetRequestHeader(Globals.Content_Type, Globals.ApplicationJson);
            www.downloadHandler = new DownloadHandlerBuffer();

            byte[] payload = Encoding.UTF8.GetBytes(data);
            UploadHandler handler = new UploadHandlerRaw(payload);
            handler.contentType = "application/json";
            www.uploadHandler = handler;
            
            return www;
        }
        

        public static IEnumerator SendScore(int score)
        { 

            //First, send the map data for this map
            using (UnityWebRequest wwwSendMap = new UnityWebRequest("https://tnh-dashboard.azure-api.net/v1/api/maps", "Put"))
            {
                wwwSendMap.SetRequestHeader(Globals.Accept, "*/*");
                wwwSendMap.SetRequestHeader(Globals.Content_Type, Globals.ApplicationJson);

                GetHoldList().ForEach(o => TNHTweakerLogger.Log($"Hold: x={o.x}, z={o.z}", TNHTweakerLogger.LogType.TNH));

                MapData mapData = new MapData()
                {
                    MapName = GM.TNH_Manager.LevelName,
                    HoldPointLocations = JsonConvert.SerializeObject(GetHoldList()),
                    SupplyPointLocations = JsonConvert.SerializeObject(GetSupplyList())
                };

                string data = JsonConvert.SerializeObject(mapData);
                wwwSendMap.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(data));
                wwwSendMap.uploadHandler.contentType = "application/json";

                yield return wwwSendMap.Send();

                if (wwwSendMap.isError)
                {
                    TNHTweakerLogger.LogError("Something bad happened sending map data! \n" + wwwSendMap.error);
                }
                else
                {
                    TNHTweakerLogger.Log("Sent map data", TNHTweakerLogger.LogType.TNH);
                }
            }


            //Now send the score
            using (UnityWebRequest wwwScores = new UnityWebRequest("https://tnh-dashboard.azure-api.net/v1/api/scores", "Put"))
            {
                wwwScores.SetRequestHeader(Globals.Accept, "*/*");
                wwwScores.SetRequestHeader(Globals.Content_Type, Globals.ApplicationJson);

                ScoreEntry entry = GetScoreEntry(GM.TNH_Manager, score);
                string data = JsonConvert.SerializeObject(entry);
                wwwScores.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(data));
                wwwScores.uploadHandler.contentType = "application/json";

                yield return wwwScores.Send();

                if (wwwScores.isError)
                {
                    TNHTweakerLogger.LogError("Something bad happened sending score! \n" + wwwScores.error);
                }
                else
                {
                    TNHTweakerLogger.Log("Sent score data", TNHTweakerLogger.LogType.TNH);
                }
            }

        }
    }
}
