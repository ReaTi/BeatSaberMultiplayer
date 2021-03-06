﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.UI;
using Harmony;
using Lidgren.Network;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace BeatSaberMultiplayer
{
    public class SpectatingController : MonoBehaviour
    {
        public static SpectatingController Instance;

        public static bool active = false;

        public Dictionary<ulong, List<PlayerInfo>> playerInfos  = new Dictionary<ulong, List<PlayerInfo>>();
        public Dictionary<ulong, List<HitData>> playersHits     = new Dictionary<ulong, List<HitData>>();

        public OnlinePlayerController spectatedPlayer;
        public AudioTimeSyncController audioTimeSync;

        private ScoreController _scoreController;
        private GameEnergyCounter _energyCounter;
        private AudioSource _songAudioSource;

        private string _currentScene;

        private OnlineVRController _leftController;
        private OnlineVRController _rightController;

        private Saber _leftSaber;
        private Saber _rightSaber;
        
        private Camera _mainCamera;

        private TextMeshPro _spectatingText;
        private TextMeshPro _bufferingText;

        private bool _paused = false;

#if DEBUG && VERBOSE
        private int _prevFrameIndex = 0;
        private float _prevFrameLerp = 0f;
#endif

        public static void OnLoad()
        {
            if (Instance != null)
                return;
            new GameObject("SpectatingController").AddComponent<SpectatingController>();

        }

        public void Awake()
        {
            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(this);

                Client.Instance.PlayerInfoUpdateReceived -= PacketReceived;
                Client.Instance.PlayerInfoUpdateReceived += PacketReceived;
                _currentScene = SceneManager.GetActiveScene().name;
            }
        }

        public void MenuSceneLoaded()
        {
            _currentScene = "MenuCore";
            active = false;
            if (!Config.Instance.SpectatorMode)
                return;
            DestroyAvatar();
            if (_spectatingText != null)
            {
                Destroy(_spectatingText);
            }
            if (_bufferingText != null)
            {
                Destroy(_bufferingText);
            }
        }

        public void GameSceneLoaded()
        {
            Plugin.log.Info("Game scene loaded");
            _currentScene = "GameCore";

            if (!Config.Instance.SpectatorMode || !Client.Instance.connected)
            {
                active = false;
                return;
            }

            StartCoroutine(Delay(5, () => {
                active = true;
            }));
            _paused = false;

            DestroyAvatar();
            ReplaceControllers();
            
            if(_spectatingText != null)
            {
                Destroy(_spectatingText);
            }

            _spectatingText = CustomExtensions.CreateWorldText(transform, "Spectating PLAYER");
            _spectatingText.alignment = TextAlignmentOptions.Center;
            _spectatingText.fontSize = 6f;
            _spectatingText.transform.position = new Vector3(0f, 3.75f, 12f);
            _spectatingText.gameObject.SetActive(false);

            if (_bufferingText != null)
            {
                Destroy(_bufferingText);
            }

            _bufferingText = CustomExtensions.CreateWorldText(transform, "Buffering...");
            _bufferingText.alignment = TextAlignmentOptions.Center;
            _bufferingText.fontSize = 8f;
            _bufferingText.transform.position = new Vector3(0f, 2f, 8f);
            _bufferingText.gameObject.SetActive(false);

#if DEBUG  && LOCALREPLAY
            string replayPath = Path.GetFullPath("MPDumps\\BootyBounce.mpdmp");

            Stream stream;
            long length;
            long position;

            if (replayPath.EndsWith(".zip"))
            {
                ZipArchiveEntry entry = new ZipArchive(File.Open(replayPath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read).Entries.First(x => x.Name.EndsWith(".mpdmp"));
                position = 0;
                length = entry.Length;
                stream = entry.Open();
            }
            else
            {
                var fileStream = File.Open(replayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                position = 0;
                length = fileStream.Length;
                stream = fileStream;
            }

            byte[] startLevelInfo = new byte[2];
            stream.Read(startLevelInfo, 0, 2);
            position += 2;

            byte[] levelIdBytes = new byte[16];
            stream.Read(levelIdBytes, 0, 16);
            position += 16;

            while (length - position > 79)
            {

                byte[] packetSizeBytes = new byte[4];
                stream.Read(packetSizeBytes, 0, 4);
                position += 4;

                int packetSize = BitConverter.ToInt32(packetSizeBytes, 0);

                byte[] packetBytes = new byte[packetSize];
                stream.Read(packetBytes, 0, packetSize);
                position += packetSize;

                PlayerInfo player = new PlayerInfo(packetBytes);
                player.playerId = 76561198047255565;
                player.playerName = "andruzzzhka";
                player.avatarHash = "1f0152521ab8aa04ea53beed79c083e6".ToUpper();

                if (playerInfos.ContainsKey(player.playerId))
                {
                    playerInfos[player.playerId].Add(player);
                    playersHits[player.playerId].AddRange(player.hitsLastUpdate);
                }
                else
                {
                    playerInfos.Add(player.playerId, new List<PlayerInfo>() { player });
                    playersHits.Add(player.playerId, new List<HitData>(player.hitsLastUpdate));
                }
            }

            Plugin.log.Info("Loaded "+ playerInfos[76561198047255565].Count + " packets!");
#endif
        }

        IEnumerator Delay(int frames, Action callback)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
            callback.Invoke();
        }

        void ReplaceControllers()
        {
            if (!Config.Instance.SpectatorMode || Client.Instance.inRadioMode)
                return;
            
            audioTimeSync = Resources.FindObjectsOfTypeAll<AudioTimeSyncController>().FirstOrDefault();
            _songAudioSource = audioTimeSync.GetPrivateField<AudioSource>("_audioSource");

            _leftSaber = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberB);
            _leftController = _leftSaber.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _leftSaber.SetPrivateField("_vrController", _leftController);

            _rightSaber = Resources.FindObjectsOfTypeAll<Saber>().First(x => x.saberType == Saber.SaberType.SaberA);
            _rightController = _rightSaber.GetPrivateField<VRController>("_vrController").gameObject.AddComponent<OnlineVRController>();
            _rightSaber.SetPrivateField("_vrController", _rightController);

            Plugin.log.Info("Controllers replaced!");

            _scoreController = FindObjectOfType<ScoreController>();

#if DEBUG
            _scoreController.noteWasMissedEvent += _scoreController_noteWasMissedEvent;
            _scoreController.noteWasCutEvent += _scoreController_noteWasCutEvent;
#endif

            Plugin.log.Info("Score controller found!");

            _energyCounter = FindObjectOfType<GameEnergyCounter>();

            Plugin.log.Info("Energy counter found!");
        }

#if DEBUG
        private void _scoreController_noteWasCutEvent(NoteData arg1, NoteCutInfo arg2, int arg3)
        {
            if(spectatedPlayer != null)
            {
                HitData hit = playersHits[spectatedPlayer.PlayerInfo.playerId].FirstOrDefault(x => Mathf.Abs(x.objectTime - arg1.time) < float.Epsilon);
                bool allIsOKExpected = hit.noteWasCut && hit.speedOK && hit.saberTypeOK && hit.directionOK && !hit.wasCutTooSoon;

                if(allIsOKExpected != arg2.allIsOK)
                {
                    Plugin.log.Error($"Replay error detected!\n|   Real hit: {arg2.allIsOK}\n|   Expected hit: {allIsOKExpected}\n|   Time: {audioTimeSync.songTime}");
                }
            }
        }

        private void _scoreController_noteWasMissedEvent(NoteData arg1, int arg2)
        {
            if (spectatedPlayer != null)
            {
                HitData hit = playersHits[spectatedPlayer.PlayerInfo.playerId].FirstOrDefault(x => Mathf.Abs(x.objectTime - arg1.time) < float.Epsilon);

                if (hit.noteWasCut)
                {
                    Plugin.log.Error($"Replay error detected!\n|   Real hit: false\n|   Expected hit: {hit.noteWasCut}\n|   Time: {audioTimeSync.songTime}");
                }
            }
        }
#endif

        private void PacketReceived(NetIncomingMessage msg)
        {
            if (Config.Instance.SpectatorMode && !Client.Instance.inRadioMode && _currentScene == "GameCore")
            {
                msg.Position = 0;
                CommandType commandType = (CommandType)msg.ReadByte();
                if (commandType == CommandType.GetPlayerUpdates)
                {
                    int playersCount = msg.ReadInt32();
                    
                    for (int j = 0; j < playersCount; j++)
                    {
                        try
                        {
                            int packetsCount = msg.ReadInt32();
                            for (int k = 0; k < packetsCount; k++)
                            {
                                PlayerInfo player = new PlayerInfo(msg);
                                if (playerInfos.ContainsKey(player.playerId))
                                {
                                    if (audioTimeSync != null && audioTimeSync.songTime > 3f)
                                    {
                                        int index = playerInfos[player.playerId].FindIndex(x => x.playerProgress < audioTimeSync.songTime - 2f);

                                        if (index > -1)
                                            playerInfos[player.playerId].RemoveRange(0, index + 1);

                                        index = playersHits[player.playerId].FindIndex(x => x.objectTime < audioTimeSync.songTime - 3f);

                                        if (index > -1)
                                            playersHits[player.playerId].RemoveRange(0, index + 1);
                                    }
                                    playerInfos[player.playerId].Add(player);
                                    playersHits[player.playerId].AddRange(player.hitsLastUpdate);
                                }
                                else
                                {
                                    playerInfos.Add(player.playerId, new List<PlayerInfo>() { player });
                                    playersHits.Add(player.playerId, player.hitsLastUpdate);
                                }
                            }
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            Plugin.log.Critical($"Unable to parse PlayerInfo! Excpetion: {e}");
#endif
                        }
                    }
                }
            }
        }

        public void DestroyAvatar()
        {
            if(spectatedPlayer != null)
            {
                Destroy(spectatedPlayer.gameObject);
            }
        }

        public void Update()
        {
            if (Config.Instance.SpectatorMode && _currentScene == "GameCore" && active)
            {
                if (spectatedPlayer == null && _leftSaber != null && _rightSaber != null)
                {
                    spectatedPlayer = new GameObject("SpectatedPlayerController").AddComponent<OnlinePlayerController>();
                    spectatedPlayer.SetAvatarState(Config.Instance.ShowAvatarsInGame);
                    spectatedPlayer.SetSabers(_leftSaber, _rightSaber);

                    ReplacePlayerController(spectatedPlayer);

                    spectatedPlayer.noInterpolation = true;

                    if (_leftController != null && _rightController != null)
                    {
                        _leftController.owner = spectatedPlayer;
                        _rightController.owner = spectatedPlayer;
                    }
                }

                if (Input.GetKeyDown(KeyCode.KeypadMultiply) && spectatedPlayer != null && spectatedPlayer.PlayerInfo != null)
                {
                    int index = playerInfos.Keys.ToList().FindIndexInList(spectatedPlayer.PlayerInfo.playerId);
                    if (index >= playerInfos.Count - 1)
                    {
                        index = 0;
                    }

                    spectatedPlayer.PlayerInfo = playerInfos[playerInfos.Keys.ElementAt(index)].Last();
                    Plugin.log.Info("Spectating player: " + spectatedPlayer.PlayerInfo.playerName);
                    _spectatingText.gameObject.SetActive(true);
                    _spectatingText.text = "Spectating " + spectatedPlayer.PlayerInfo.playerName;
                }

                if (Input.GetKeyDown(KeyCode.KeypadDivide) && spectatedPlayer != null && spectatedPlayer.PlayerInfo != null)
                {
                    int index = playerInfos.Keys.ToList().FindIndexInList(spectatedPlayer.PlayerInfo.playerId);
                    if (index <= 0)
                    {
                        index = playerInfos.Count - 1;
                    }

                    spectatedPlayer.PlayerInfo = playerInfos[playerInfos.Keys.ElementAt(index)].Last();
                    Plugin.log.Info("Spectating player: " + spectatedPlayer.PlayerInfo.playerName);
                    _spectatingText.gameObject.SetActive(true);
                    _spectatingText.text = "Spectating " + spectatedPlayer.PlayerInfo.playerName;
                }


                if (playerInfos.Count > 0 && spectatedPlayer != null && spectatedPlayer.PlayerInfo == null)
                {
                    spectatedPlayer.PlayerInfo = playerInfos.FirstOrDefault(x => !x.Key.Equals(Client.Instance.playerInfo)).Value?.LastOrDefault();
                    if (spectatedPlayer.PlayerInfo != null)
                    {
                        Plugin.log.Info("Spectating player: " + spectatedPlayer.PlayerInfo.playerName);
                        _spectatingText.gameObject.SetActive(true);
                        _spectatingText.text = "Spectating " + spectatedPlayer.PlayerInfo.playerName;
                    }
                }

                if (spectatedPlayer != null && spectatedPlayer.PlayerInfo != null)
                {
                    float currentSongTime = Math.Max(0f, audioTimeSync.songTime);
                    int index = FindClosestIndex(playerInfos[spectatedPlayer.PlayerInfo.playerId], currentSongTime);
                    index = Math.Max(index, 0);
                    (float, float) playerProgressMinMax = MinMax(playerInfos[spectatedPlayer.PlayerInfo.playerId]);
                    PlayerInfo lerpTo;
                    float lerpProgress;


                    if (playerProgressMinMax.Item2 < currentSongTime + 2f && audioTimeSync.songLength > currentSongTime + 5f && !_paused)
                    {
                        Plugin.log.Info($"Pausing...");
                        if (playerProgressMinMax.Item2 > 2.5f)
                        {
                            Plugin.log.Info($"Buffering...");
                            _bufferingText.gameObject.SetActive(true);
                            _bufferingText.alignment = TextAlignmentOptions.Center;
                        }
                        InGameOnlineController.Instance.PauseSong();
                        _paused = true;
                        Plugin.log.Info($"Paused!");
                    }

                    if (playerProgressMinMax.Item2 - currentSongTime > 3f && _paused)
                    {
                        _bufferingText.gameObject.SetActive(false);
                        Plugin.log.Info("Resuming song...");
                        InGameOnlineController.Instance.ResumeSong();
                        _paused = false;
                    }

                    if (_paused)
                        return;

                    if (playerProgressMinMax.Item1 < currentSongTime && playerProgressMinMax.Item2 > currentSongTime)
                    {
                        spectatedPlayer.PlayerInfo = playerInfos[spectatedPlayer.PlayerInfo.playerId][index];
                        lerpTo = playerInfos[spectatedPlayer.PlayerInfo.playerId][index + 1];

                        lerpProgress = Remap(currentSongTime, spectatedPlayer.PlayerInfo.playerProgress, lerpTo.playerProgress, 0f, 1f);
                    }
                    else
                    {
                        if (audioTimeSync.songLength - currentSongTime > 5f && currentSongTime > 3f)
                        {
                            Plugin.log.Warn($"No data recorded for that point in time!\nStart time: {playerProgressMinMax.Item1}\nStop time: {playerProgressMinMax.Item2}\nCurrent time: {currentSongTime}");
                        }
                        return;
                    }

#if DEBUG && VERBOSE
                    if (index - _prevFrameIndex > 1)
                    {
                        Plugin.log.Warn($"Frame skip!\nPrev index: {_prevFrameIndex}\nNew index: {index}");
                    }
                    else if (index < _prevFrameIndex)
                    {
                        Plugin.log.Warn($"Going back in time!\nPrev index: {_prevFrameIndex}\nNew index: {index}");
                    }
                    else if (_prevFrameIndex == index)
                    {
                        if (lerpProgress < _prevFrameLerp)
                        {
                            Plugin.log.Warn($"Going back in time!\nPrev lerp progress: {_prevFrameIndex}\nNew lerp progress: {lerpProgress}");
                        }
                        else if (_prevFrameLerp == lerpProgress)
                        {
                            Plugin.log.Warn($"Staying in place! Prev lerp progress: {_prevFrameLerp}\nNew  lerp progress: {lerpProgress}");
                        }
                    }
                    _prevFrameIndex = index;
                    _prevFrameLerp = lerpProgress;
#endif

                    spectatedPlayer.PlayerInfo.leftHandPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.leftHandPos, lerpTo.leftHandPos, lerpProgress);
                    spectatedPlayer.PlayerInfo.rightHandPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.rightHandPos, lerpTo.rightHandPos, lerpProgress);
                    spectatedPlayer.PlayerInfo.headPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.headPos, lerpTo.headPos, lerpProgress);

#if DEBUG
                    if (Environment.CommandLine.ToLower().Contains("fpfc"))
                    {
                        if (_mainCamera == null)
                        {
                            _mainCamera = Camera.main;
                            XRSettings.showDeviceView = false;
                        }
                        _mainCamera.transform.position = spectatedPlayer.PlayerInfo.headPos;
                    }
#endif

                    spectatedPlayer.PlayerInfo.leftHandRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.leftHandRot, lerpTo.leftHandRot, lerpProgress);
                    spectatedPlayer.PlayerInfo.rightHandRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.rightHandRot, lerpTo.rightHandRot, lerpProgress);
                    spectatedPlayer.PlayerInfo.headRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.headRot, lerpTo.headRot, lerpProgress);

                    if (spectatedPlayer.PlayerInfo.fullBodyTracking)
                    {
                        spectatedPlayer.PlayerInfo.leftLegPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.leftLegPos, lerpTo.leftLegPos, lerpProgress);
                        spectatedPlayer.PlayerInfo.rightLegPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.rightLegPos, lerpTo.rightLegPos, lerpProgress);
                        spectatedPlayer.PlayerInfo.pelvisPos = Vector3.Lerp(spectatedPlayer.PlayerInfo.pelvisPos, lerpTo.pelvisPos, lerpProgress);

                        spectatedPlayer.PlayerInfo.leftLegRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.leftLegRot, lerpTo.leftLegRot, lerpProgress);
                        spectatedPlayer.PlayerInfo.rightLegRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.rightLegRot, lerpTo.rightLegRot, lerpProgress);
                        spectatedPlayer.PlayerInfo.pelvisRot = Quaternion.Lerp(spectatedPlayer.PlayerInfo.pelvisRot, lerpTo.pelvisRot, lerpProgress);
                    }

                    if (_scoreController != null)
                    {
                        _scoreController.SetPrivateField("_prevFrameRawScore", (int)spectatedPlayer.PlayerInfo.playerScore);
                        _scoreController.SetPrivateField("_baseRawScore", (int)lerpTo.playerScore);
                        _scoreController.SetPrivateField("_combo", (int)lerpTo.playerComboBlocks);
                    }

                    if(_energyCounter != null)
                    {
                        _energyCounter.SetPrivateProperty("energy", lerpTo.playerEnergy / 100f);
                    }

                }
            }
        }

        private void ReplacePlayerController(PlayerController newPlayerController)
        {
            Type[] typesToReplace = new Type[] { typeof(BombCutSoundEffectManager), typeof(NoteCutSoundEffectManager), typeof(MoveBackWall), typeof(NoteFloorMovement), typeof(NoteJump), typeof(NoteLineConnectionController), typeof(ObstacleController), typeof(PlayerHeadAndObstacleInteraction) };

            foreach(Type type in typesToReplace)
            {
                Resources.FindObjectsOfTypeAll(type).ToList().ForEach(x => x.SetPrivateField("_playerController", newPlayerController));
            }
        }

        public float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        private static int FindClosestIndex(List<PlayerInfo> infos, float targetProgress)
        {
            for (int i = 0; i < infos.Count - 1; i++)
            {
                if ((infos[i].playerProgress < targetProgress && infos[i+1].playerProgress > targetProgress) || Mathf.Abs(infos[i].playerProgress - targetProgress) < float.Epsilon)
                {
                    return i;
                }
            }
            return -1;
        }

        private static (float, float) MinMax(List<PlayerInfo> infos)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach(PlayerInfo info in infos)
            {
                if (info.playerProgress > max)
                    max = info.playerProgress;
                if (info.playerProgress < min)
                    min = info.playerProgress;
            }

            return (min, max);
        }
    }
}
