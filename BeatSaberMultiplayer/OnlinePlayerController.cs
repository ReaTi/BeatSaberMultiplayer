using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.OverriddenClasses;
using BS_Utils.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class OnlinePlayerController : PlayerController
    {
        public PlayerInfo PlayerInfo {

            get
            {
                return _info;
            }

            set
            {
                UpdatePrevPosRot(value);
                if (_info != null && value != null)
                {
                    _info.playerName = value.playerName;
                    _info.playerName = value.playerName;
                    _info.avatarHash = value.avatarHash;
                    _info.playerComboBlocks = value.playerComboBlocks;
                    _info.playerCutBlocks = value.playerCutBlocks;
                    _info.playerEnergy = value.playerEnergy;
                    _info.playerScore = value.playerScore;
                    _info.playerState = value.playerState;
                    _info.playerTotalBlocks = value.playerTotalBlocks;
                }
                else
                {
                    _info = value;
                }
            }
        }

        public AvatarController avatar;
        public AudioSource voipSource;

        public OnlineBeatmapCallbackController beatmapCallbackController;
        public OnlineBeatmapSpawnController beatmapSpawnController;
        public OnlineAudioTimeController audioTimeController;

        public float avatarOffset;
        public bool noInterpolation = false;
        public bool destroyed = false;
        
        private PlayerInfo _info;
        private AudioClip _voipClip;
        private float[] _voipBuffer;
        private int _lastVoipFragIndex;
        private int _silentFrames;

        public float syncDelay = 0f;
        private float lastSynchronizationTime = 0f;
        private float syncTime = 0f;
        private float lerpProgress = 0f;

        private PlayerInfo syncStartInfo;
        private PlayerInfo syncEndInfo;

        public void Start()
        {
#if DEBUG
            Plugin.log.Info($"Player controller created!");
#endif
            voipSource = gameObject.AddComponent<AudioSource>();

            _voipClip = AudioClip.Create("VoIP Clip", 65535, 1, 16000, false);
            voipSource.clip = _voipClip;
            voipSource.spatialize = Config.Instance.SpatialAudio;

            if (_info != null)
            {
#if DEBUG
                Plugin.log.Info($"Starting player controller for {_info.playerName}:{_info.playerId}...");
#endif
                syncStartInfo = _info;
                syncEndInfo = _info;
            }

            if (SceneManager.GetActiveScene().name == "GameCore" && Config.Instance.ShowOtherPlayersBlocks && !Client.Instance.playerInfo.Equals(PlayerInfo) && !Config.Instance.SpectatorMode)
            {
                SpawnBeatmapControllers();
                SpawnSabers();
            }
        }

        void SpawnBeatmapControllers()
        {
            Plugin.log.Info("Creating beatmap controllers...");

            beatmapCallbackController = new GameObject("OnlineBeatmapCallbackController").AddComponent<OnlineBeatmapCallbackController>();
            Plugin.log.Info("Created beatmap callback controller!");
            beatmapCallbackController.Init(this);
            Plugin.log.Info("Initialized beatmap callback controller!");
            
            audioTimeController = new GameObject("OnlineAudioTimeController").AddComponent<OnlineAudioTimeController>();
            Plugin.log.Info("Created audio time controller!");
            audioTimeController.Init(this);
            Plugin.log.Info("Initialized audio time controller!");

            beatmapSpawnController = new GameObject("OnlineBeatmapSpawnController").AddComponent<OnlineBeatmapSpawnController>();
            Plugin.log.Info("Created beatmap spawn controller!");
            beatmapSpawnController.Init(this, beatmapCallbackController, audioTimeController);
            Plugin.log.Info("Initialized beatmap spawn controller!");
        }

        void SpawnSabers()
        {
            Plugin.log.Info("Spawning left saber...");
            _leftSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "LeftSaber"), transform, false);//.gameObject.AddComponent<OnlineSaber>();
            _leftSaber.gameObject.name = "CustomLeftSaber";
            var leftController = _leftSaber.gameObject.AddComponent<OnlineVRController>();
            leftController.owner = this;
            _leftSaber.SetPrivateField("_vrController", leftController);

            var leftTrail = leftController.GetComponentInChildren<SaberWeaponTrail>();
            var colorManager = Resources.FindObjectsOfTypeAll<ColorManager>().First();
            leftTrail.SetPrivateField("_colorManager", colorManager);
            leftTrail.SetPrivateField("_saberTypeObject", leftController.GetComponentInChildren<SaberTypeObject>());

            Plugin.log.Info("Spawning right saber...");
            _rightSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "RightSaber"), transform, false);//.gameObject.AddComponent<OnlineSaber>();
            _rightSaber.gameObject.name = "CustomRightSaber";
            var rightController = _rightSaber.gameObject.AddComponent<OnlineVRController>();
            rightController.owner = this;
            _rightSaber.SetPrivateField("_vrController", rightController);

            var rightTrail = rightController.GetComponentInChildren<SaberWeaponTrail>();
            rightTrail.SetPrivateField("_colorManager", colorManager);
            rightTrail.SetPrivateField("_saberTypeObject", rightController.GetComponentInChildren<SaberTypeObject>());


            Plugin.log.Info("Sabers spawned!");
        }

        public void SetSabers(Saber leftSaber, Saber rightSaber)
        {
            _leftSaber = leftSaber;
            _rightSaber = rightSaber;
        }

        public override void Update()
        {
            if (avatar != null)
            {
                avatar.SetPlayerInfo(_info, avatarOffset, Client.Instance.playerInfo.Equals(_info));
            }

            if (voipSource != null)
            {
                if (!voipSource.isPlaying)
                {
                    _silentFrames++;
                }
                else
                {
                    _silentFrames = 0;
                }
            }
            else
            {
                _silentFrames = 999;
            }

            if (_rightSaber != null)
            {
                _rightSaber.ManualUpdate();
            }

            if (_leftSaber != null)
            {
                _leftSaber.ManualUpdate();
            }
        }

        public void FixedUpdate()
        {
            if (_info != null)
            {
                if (syncStartInfo != null && syncEndInfo != null && !noInterpolation)
                {
                    syncTime += Time.fixedDeltaTime;

                    lerpProgress = syncTime / syncDelay;

                    _info.headPos = Vector3.Lerp(syncStartInfo.headPos, syncEndInfo.headPos, lerpProgress);
                    _info.leftHandPos = Vector3.Lerp(syncStartInfo.leftHandPos, syncEndInfo.leftHandPos, lerpProgress);
                    _info.rightHandPos = Vector3.Lerp(syncStartInfo.rightHandPos, syncEndInfo.rightHandPos, lerpProgress);
                    _info.leftLegPos = Vector3.Lerp(syncStartInfo.leftLegPos, syncEndInfo.leftLegPos, lerpProgress);
                    _info.rightLegPos = Vector3.Lerp(syncStartInfo.rightLegPos, syncEndInfo.rightLegPos, lerpProgress);
                    _info.pelvisPos = Vector3.Lerp(syncStartInfo.pelvisPos, syncEndInfo.pelvisPos, lerpProgress);

                    _info.headRot = Quaternion.Lerp(syncStartInfo.headRot, syncEndInfo.headRot, lerpProgress);
                    _info.leftHandRot = Quaternion.Lerp(syncStartInfo.leftHandRot, syncEndInfo.leftHandRot, lerpProgress);
                    _info.rightHandRot = Quaternion.Lerp(syncStartInfo.rightHandRot, syncEndInfo.rightHandRot, lerpProgress);
                    _info.leftLegRot = Quaternion.Lerp(syncStartInfo.leftLegRot, syncEndInfo.leftLegRot, lerpProgress);
                    _info.rightLegRot = Quaternion.Lerp(syncStartInfo.rightLegRot, syncEndInfo.rightLegRot, lerpProgress);
                    _info.pelvisRot = Quaternion.Lerp(syncStartInfo.pelvisRot, syncEndInfo.pelvisRot, lerpProgress);

                    float lerpedPlayerProgress = Mathf.Lerp(syncStartInfo.playerProgress, syncEndInfo.playerProgress, lerpProgress);

                    if(_info.playerProgress < lerpedPlayerProgress && Mathf.Abs(_info.playerProgress - lerpedPlayerProgress) < 0.5f)
                    {
                        _info.playerProgress = lerpedPlayerProgress;
                    }
                }

                _overrideHeadPos = true;
                _overriddenHeadPos = _info.headPos;
                _headPos = _info.headPos + Vector3.right * avatarOffset;
                transform.position = _headPos;
            }
        }

        public void OnDestroy()
        {
#if DEBUG
            if(_info == null)
                Plugin.log.Info("Destroying player controller!");
            else
                Plugin.log.Info($"Destroying player controller! Name: {_info.playerName}, ID: {_info.playerId}");
#endif
            destroyed = true;
            
            if (avatar != null)
            {
                Destroy(avatar.gameObject);
            }

            if (beatmapCallbackController != null && beatmapSpawnController != null)
            {
                Destroy(beatmapCallbackController.gameObject);
                Destroy(beatmapSpawnController.gameObject);
            }
        }

        public void UpdatePrevPosRot(PlayerInfo newPlayerInfo)
        {
            if (newPlayerInfo == null || _info == null)
                return;

            if (noInterpolation)
            {
                _info = newPlayerInfo;
                return;
            }
            
            syncStartInfo = new PlayerInfo(_info);
            if (syncStartInfo.IsRotNaN())
            {
                syncStartInfo.headRot = Quaternion.identity;
                syncStartInfo.leftHandRot = Quaternion.identity;
                syncStartInfo.rightHandRot = Quaternion.identity;
                syncStartInfo.leftLegRot = Quaternion.identity;
                syncStartInfo.rightLegRot = Quaternion.identity;
                syncStartInfo.pelvisRot = Quaternion.identity;
                Plugin.log.Warn("Start rotation is NaN!");
            }

            if (Mathf.Abs(_info.playerProgress - newPlayerInfo.playerProgress) > 0.1f)
            {
                _info.playerProgress = newPlayerInfo.playerProgress;
            }

            syncEndInfo = newPlayerInfo;
            if (syncEndInfo.IsRotNaN())
            {
                syncEndInfo.headRot = Quaternion.identity;
                syncEndInfo.leftHandRot = Quaternion.identity;
                syncEndInfo.rightHandRot = Quaternion.identity;
                syncEndInfo.leftLegRot = Quaternion.identity;
                syncEndInfo.rightLegRot = Quaternion.identity;
                syncEndInfo.pelvisRot = Quaternion.identity;
                Plugin.log.Warn("Target rotation is NaN!");
            }
            
            syncTime = 0;
            syncDelay = Time.time - lastSynchronizationTime;

            if(syncDelay > 0.5f)
            {
                syncDelay = 0.5f;
            }

            lastSynchronizationTime = Time.time;

        }

        public void SetAvatarState(bool enabled)
        {
            if(enabled && (object)avatar == null)
            {
                avatar = new GameObject("AvatarController").AddComponent<AvatarController>();
                avatar.SetPlayerInfo(_info, avatarOffset, Client.Instance.playerInfo.Equals(_info));
            }
            else if(!enabled && avatar != null)
            {
                Destroy(avatar.gameObject);
                avatar = null;
            }
        }

        public void PlayVoIPFragment(float[] data, int fragIndex)
        {
            if(voipSource != null && !InGameOnlineController.Instance.mutedPlayers.Contains(_info.playerId))
            {
                if (_voipBuffer == null || (_lastVoipFragIndex + 1) != fragIndex || _silentFrames > 20)
                {
                    float[] tempBuffer = new float[data.Length + 1024];

                    Buffer.BlockCopy(data, 0, tempBuffer, 1023 * sizeof(float), data.Length * sizeof(float));

                    _voipBuffer = tempBuffer;
                    _lastVoipFragIndex = fragIndex;
                    _voipClip.SetData(_voipBuffer, 0);
                    voipSource.Play();
                    _silentFrames = 0;

                }
                else
                {
                    int currentPos = voipSource.timeSamples;

                    if (currentPos >= _voipBuffer.Length)
                        currentPos = _voipBuffer.Length - 1;
                    if (currentPos < 1)
                        currentPos = 1;

                    float[] tempBuffer = new float[_voipBuffer.Length - currentPos - 1 + data.Length];

                    Buffer.BlockCopy(_voipBuffer, (currentPos - 1) * sizeof(float), tempBuffer, 0,  (_voipBuffer.Length - currentPos - 1) * sizeof(float));
                    Buffer.BlockCopy(data, 0, tempBuffer, (_voipBuffer.Length - currentPos - 1) * sizeof(float), data.Length * sizeof(float));

                    _voipBuffer = tempBuffer;
                    _lastVoipFragIndex = fragIndex;
                    _voipClip.SetData(_voipBuffer, 0);
                    voipSource.Play();
                    _silentFrames = 0;
                }
            }
        }

        public void SetVoIPVolume(float newVolume)
        {
            if(voipSource != null)
            {
                voipSource.volume = newVolume;
            }
        }

        public void SetSpatialAudioState(bool spatialAudio)
        {
            if (voipSource != null)
            {
                voipSource.spatialize = spatialAudio;
            }
        }

        public bool IsTalking()
        {
            return _silentFrames < 20;
        }
    }
}
