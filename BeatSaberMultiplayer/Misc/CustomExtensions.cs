﻿using BeatSaberMultiplayer.Data;
using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.Misc
{
    public static class CustomExtensions
    {
        private static Shader _customTextShader;
        public static Shader CustomTextShader
        {
            get
            {
                if(_customTextShader == null)
                {
                    Plugin.log.Info("Loading text shader asset bundle...");
                    AssetBundle assetBundle = AssetBundle.LoadFromStream(Assembly.GetCallingAssembly().GetManifestResourceStream("BeatSaberMultiplayer.Assets.Shader.asset"));
                    _customTextShader = assetBundle.LoadAsset<Shader>("Assets/TextMesh Pro/Resources/Shaders/TMP_SDF_ZeroAlphaWrite_ZWrite.shader");
                }
                return _customTextShader;
            }
        }

        public static List<string> basePackIDs = new List<string>() { "OstVol1", "OstVol2", "Extras" };

        public static Sprite songLoaderDefaultImage {
            get
            {
                if (_songLoaderDefaultImage == null) {
                    Type type = typeof(SongLoaderPlugin.SongLoader);
                    FieldInfo info = type.GetField("CustomSongsIcon", BindingFlags.NonPublic | BindingFlags.Static);
                    _songLoaderDefaultImage = (Sprite)info.GetValue(null);
                }

                return _songLoaderDefaultImage ?? Sprites.whitePixel;
            }
        }
        private static Sprite _songLoaderDefaultImage;

        public static void SetButtonStrokeColor(this Button btn, Color color)
        {
            btn.GetComponentsInChildren<UnityEngine.UI.Image>().First(x => x.name == "Stroke").color = color;
        }

        public static IDifficultyBeatmap GetDifficultyBeatmap(this BeatmapLevelSO level, BeatmapCharacteristicSO characteristic, BeatmapDifficulty difficulty, bool strictDifficulty = false)
        {
            IDifficultyBeatmapSet difficultySet = null;
            if (characteristic == null)
            {
                difficultySet = level.difficultyBeatmapSets.FirstOrDefault();
            }
            else
            {
                difficultySet = level.difficultyBeatmapSets.FirstOrDefault(x => x.beatmapCharacteristic == characteristic);
            }

            if (difficultySet == null)
            {
                return null;
            }

            IDifficultyBeatmap beatmap = difficultySet.difficultyBeatmaps.FirstOrDefault(x => x.difficulty == difficulty);

            if(beatmap == null && !strictDifficulty)
            {
                return difficultySet.difficultyBeatmaps[GetClosestDifficultyIndex(difficultySet.difficultyBeatmaps, difficulty)];
            }
            else
            {
                return beatmap;
            }
        }

        public static int GetClosestDifficultyIndex(IDifficultyBeatmap[] beatmaps, BeatmapDifficulty difficulty)
        {
            int num = -1;
            foreach (IDifficultyBeatmap difficultyBeatmap in beatmaps)
            {
                if (difficulty < difficultyBeatmap.difficulty)
                {
                    break;
                }
                num++;
            }
            if (num == -1)
            {
                num = 0;
            }
            return num;
        }

        public static int FindIndexInList<T>(this List<T> list, T b)
        {
            return list.FindIndex(x => x.Equals(b));
        }

        public static int FindIndexInArray<T>(this T[] list, T b)
        {
            for(int i = 0; i < list.Length; i++)
            {
                if ((list[i] == null && b == null) || (list[i] != null && list[i].Equals(b)))
                    return i;
            }
            return -1;
        }

        public static TextMeshPro CreateWorldText(Transform parent, string text="TEXT")
        {
            GameObject textMeshGO = new GameObject("CustomUIText");
            
            textMeshGO.SetActive(false);

            TextMeshPro textMesh = textMeshGO.AddComponent<TextMeshPro>();
            TMP_FontAsset font = UnityEngine.Object.Instantiate(Resources.FindObjectsOfTypeAll<TMP_FontAsset>().First(x => x.name == "Teko-Medium SDF No Glow"));
            textMesh.renderer.sharedMaterial = font.material;
            textMesh.fontSharedMaterial = font.material;
            textMesh.font = font;

            textMesh.transform.SetParent(parent, true);
            textMesh.text = text;
            textMesh.fontSize = 5f;
            textMesh.color = Color.white;
            textMesh.renderer.material.shader = CustomTextShader;

            textMesh.gameObject.SetActive(true);

            return textMesh;
        }

        public static bool IsRotNaN(this PlayerInfo _info)
        {
            return  float.IsNaN(_info.headRot.x)        || float.IsNaN(_info.headRot.y)         || float.IsNaN(_info.headRot.z)         || float.IsNaN(_info.headRot.w) ||
                    float.IsNaN(_info.leftHandRot.x)    || float.IsNaN(_info.leftHandRot.y)     || float.IsNaN(_info.leftHandRot.z)     || float.IsNaN(_info.leftHandRot.w) ||
                    float.IsNaN(_info.rightHandRot.x)   || float.IsNaN(_info.rightHandRot.y)    || float.IsNaN(_info.rightHandRot.z)    || float.IsNaN(_info.rightHandRot.w);
        }

        public static T CreateInstance<T>(params object[] args)
        {
            var type = typeof(T);
            var instance = type.Assembly.CreateInstance(
                type.FullName, false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null, args, null, null);
            return (T)instance;
        }

        public static T Random<T>(this List<T> list)
        {
            return list[(int)Mathf.Round(UnityEngine.Random.Range(0, list.Count))];
        }

        public static T Random<T>(this T[] list)
        {
            return list[(int)Mathf.Round(UnityEngine.Random.Range(0, list.Length))];
        }

        public static byte[] ToBytes(this BitArray bits)
        {
            byte[] bytes = new byte[(int)Math.Ceiling(bits.Count/(double)8)];
            bits.CopyTo(bytes, 0);
            return bytes;
        }

        public static void ToShortArray(this float[] input, short[] output, int offset, int len)
        {
            for (int i = 0; i < len; ++i)
            {
                output[i] = (short)Mathf.Clamp((int)(input[i + offset] * 32767.0f), short.MinValue, short.MaxValue);
            }
        }

        public static void ToFloatArray(this short[] input, float[] output, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                output[i] = input[i] / (float)short.MaxValue;
            }
        }
    }
}
