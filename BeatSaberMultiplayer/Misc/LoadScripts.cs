﻿using HMUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BeatSaberMultiplayer.Misc
{
    class LoadScripts
    {
        static public Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();

        static public IEnumerator LoadSpriteCoroutine(string spritePath, Action<Sprite> done)
        {
            Texture2D tex;

            if (_cachedSprites.ContainsKey(spritePath))
            {
                done?.Invoke(_cachedSprites[spritePath]);
                yield break;
            }

            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(spritePath))
            {
                yield return www.SendWebRequest();

                if (www.isHttpError || www.isNetworkError)
                {
                    Plugin.log.Warn("Unable to download sprite! Exception: "+www.error);
                }
                else
                {
                    tex = DownloadHandlerTexture.GetContent(www);
                    var newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
                    _cachedSprites.Add(spritePath, newSprite);
                    done?.Invoke(newSprite);
                }
            }
        }

    }
}
