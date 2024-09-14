using JetBrains.Annotations;
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Narazaka.SimplePlatformPlayerCount.PlayerCount
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerCounter : UdonSharpBehaviour
    {
        [SerializeField]
        UdonBehaviour OnCountChangedCallback;
        [SerializeField]
        PlayerCounterChild PlayerCounterChild;
#if DEBUG_PLAYER_COUNTER
        [SerializeField]
        UnityEngine.UI.Text DebugLog;
#endif

        [UdonSynced, FieldChangeCallback(nameof(CurrentPlayerId))]
        short _CurrentPlayerId = -1;
        short CurrentPlayerId
        {
            get { return _CurrentPlayerId; }
            set
            {
#if DEBUG_PLAYER_COUNTER
            Log($"CurrentPlayerId {_CurrentPlayerId} => {value}");
#endif
                _CurrentPlayerId = value;
                if (!IsOwner() && value == Networking.LocalPlayer.playerId)
                {
                    PlayerCounterChild.NotifyInfo();
                }
            }
        }

        [UdonSynced, FieldChangeCallback(nameof(MobilePlayerIds))]
        short[] _MobilePlayerIds = new short[0];
        short[] MobilePlayerIds
        {
            get { return _MobilePlayerIds; }
            set
            {
#if DEBUG_PLAYER_COUNTER
                Log($"MobilePlayerIds {_MobilePlayerIds.Length} => {value.Length} [{ArrayToString(_MobilePlayerIds)}] => [{ArrayToString(value)}]");
#endif
                _MobilePlayerIds = value;
                UpdateCounts();
            }
        }

        short[] UnknownPlayerIds = new short[0];

        [PublicAPI, NonSerialized]
        public int PlayerCount;
        [PublicAPI, NonSerialized]
        public int PCPlayerCount;
        [PublicAPI, NonSerialized]
        public int MobilePlayerCount;

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
#if DEBUG_PLAYER_COUNTER
            Log($"OnPlayerJoined [{player.playerId}]");
#endif
            SetPlayerCount(VRCPlayerApi.GetPlayerCount());
            if (!IsOwner()) return;

            // only owner

            // owner(me) joined
            if (player.playerId == Networking.LocalPlayer.playerId)
            {
#if DEBUG_PLAYER_COUNTER
                Log("OnPlayerJoined owner");
#endif
#if UNITY_ANDROID || UNITY_IOS
                AddMobileId(Networking.LocalPlayer.playerId);
                RequestSerialization();
#endif
            }
            // other joined
            else
            {
                PushUnknownId(player.playerId);
                TrySendNextId();
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
#if DEBUG_PLAYER_COUNTER
            Log("OnPlayerLeft");
#endif
            SetPlayerCount(VRCPlayerApi.GetPlayerCount() - 1);
            if (!IsOwner()) return;

            // only owner

            var playerId = player.playerId;
            RemoveUnknownId(playerId);
            if (CurrentPlayerId == playerId)
            {
#if DEBUG_PLAYER_COUNTER
                Log("OnPlayerLeft CurrentPlayerId == playerId");
#endif
                CompleteSendCurrentId();
                TrySendNextId();
            }
            RemoveMobileId(playerId);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
#if DEBUG_PLAYER_COUNTER
            Log($"OnOwnershipTransferred {player.playerId}");
#endif
            if (IsOwner())
            {
                CheckCurrentIds();
                ReRunCheck();
            }
        }

        [PublicAPI]
        public void OnNotified()
        {
            if (!IsOwner()) return;

            // only owner

#if DEBUG_PLAYER_COUNTER
            Log("OnNotified");
#endif
            // it is no problem to add unexpected quest player id
            if (PlayerCounterChild.IsMobile) AddMobileId(PlayerCounterChild.PlayerId);
            if (PlayerCounterChild.PlayerId != CurrentPlayerId)
            {
#if DEBUG_PLAYER_COUNTER
                Log($"OnNotified {PlayerCounterChild.PlayerId} != {CurrentPlayerId}");
#endif
                return;
            }
            CompleteSendCurrentId();
            TrySendNextId();
        }

        void CheckCurrentIds()
        {
            Log($"CheckCurrentIds");
            var playerCount = VRCPlayerApi.GetPlayerCount();
            var playerIds = new short[playerCount];
            var players = new VRCPlayerApi[playerCount];
            VRCPlayerApi.GetPlayers(players);
            for (var i = 0; i < playerCount; i++)
            {
                playerIds[i] = (short)players[i].playerId;
            }
            var len =MobilePlayerIds.Length;
            var validMobileIds = new short[len];
            var newIndex = 0;
            for (var i = 0; i < len; i++)
            {
                if (Array.IndexOf(playerIds, MobilePlayerIds[i]) != -1)
                {
                    validMobileIds[newIndex] = MobilePlayerIds[i];
                    newIndex++;
                }
            }
            var newIds = new short[newIndex];
            Array.Copy(validMobileIds, newIds, newIndex);
            MobilePlayerIds = newIds;
        }

        /// <summary>
        /// reset values if owner changed
        /// </summary>
        /// <returns>true if owner changed</returns>
        void ReRunCheck()
        {
            var ownerPlayerId = Networking.LocalPlayer.playerId;
#if DEBUG_PLAYER_COUNTER
            Log($"ReRunCheck run");
#endif
            // rebuild UnknownPlayerIds > max checked id
            var latestMaxId = MaxMobileId();
            var playerCount = VRCPlayerApi.GetPlayerCount();
            var newIds = new short[playerCount];
            VRCPlayerApi[] players = new VRCPlayerApi[playerCount];
            VRCPlayerApi.GetPlayers(players);
            var newIndex = 0;
            for (var i = 0; i < playerCount; i++)
            {
                if (players[i].playerId > latestMaxId && players[i].playerId != ownerPlayerId)
                {
                    newIds[newIndex] = (short)players[i].playerId;
                    newIndex++;
                }
            }
            UnknownPlayerIds = new short[newIndex];
            Array.Copy(newIds, UnknownPlayerIds, newIndex);
#if DEBUG_PLAYER_COUNTER
            Log($"CheckOwner UnknownPlayerIds={UnknownPlayerIds.Length} [{ArrayToString(UnknownPlayerIds)}]");
#endif
            // re-run check
            CurrentPlayerId = -1;
#if UNITY_ANDROID || UNITY_IOS
            AddMobileId(Networking.LocalPlayer.playerId);
#endif
            RequestSerialization();
            SendCustomEventDelayedSeconds(nameof(TrySendNextId), 5);
        }

        void SetPlayerCount(int playerCount)
        {
            PlayerCount = playerCount;
#if DEBUG_PLAYER_COUNTER
            Log($"SetPlayerCount => {PlayerCount}");
#endif
            UpdateCounts();
        }

        void UpdateCounts()
        {
            MobilePlayerCount = _MobilePlayerIds.Length;
            PCPlayerCount = PlayerCount - MobilePlayerCount;
            OnCountChanged();
        }

        public void TrySendNextId()
        {
#if DEBUG_PLAYER_COUNTER
            Log("TrySendNextId");
#endif
            if (CurrentPlayerId != -1 || UnknownPlayerIds.Length == 0) return;
            CurrentPlayerId = UnknownPlayerIds[0];
#if DEBUG_PLAYER_COUNTER
            Log($"TrySendNextId run CurrentPlayerId={CurrentPlayerId}");
#endif
            RequestSerialization();
        }

        void CompleteSendCurrentId()
        {
#if DEBUG_PLAYER_COUNTER
            Log($"CompleteSendCurrentId CurrentPlayerId={CurrentPlayerId}");
#endif
            PopUnknownId();
            CurrentPlayerId = -1;
            RequestSerialization();
        }

        void AddMobileId(int playerId)
        {
            var newIds = AddToArray(MobilePlayerIds, (short)playerId);
#if DEBUG_PLAYER_COUNTER
            Log($"AddMobileId {newIds.Length != MobilePlayerIds.Length}");
#endif
            if (newIds.Length == MobilePlayerIds.Length) return;
            MobilePlayerIds = newIds;
#if DEBUG_PLAYER_COUNTER
            Log($"AddMobileId MobilePlayerIds={MobilePlayerIds.Length}");
#endif
            RequestSerialization();
        }

        void RemoveMobileId(int playerId)
        {
#if DEBUG_PLAYER_COUNTER
            Log("RemoveMobileId");
#endif
            var newIds = RemoveFromArray(MobilePlayerIds, (short)playerId);
            if (newIds.Length == MobilePlayerIds.Length) return;
            MobilePlayerIds = newIds;
#if DEBUG_PLAYER_COUNTER
            Log($"RemoveMobileId MobilePlayerIds={MobilePlayerIds.Length}");
#endif
            RequestSerialization();
        }

        short MaxMobileId()
        {
            if (MobilePlayerIds.Length == 0) return 0;
            return MobilePlayerIds[MobilePlayerIds.Length - 1];
        }

        void PushUnknownId(int playerId)
        {
#if DEBUG_PLAYER_COUNTER
            Log($"PushUnknownId playerId={playerId}");
#endif
            UnknownPlayerIds = AddToArray(UnknownPlayerIds, (short)playerId);
        }

        void PopUnknownId()
        {
            if (UnknownPlayerIds.Length == 0) return;
#if DEBUG_PLAYER_COUNTER
            Log($"PopUnknownId playerId={UnknownPlayerIds[0]}");
#endif
            var newIds = new short[UnknownPlayerIds.Length - 1];
            Array.Copy(UnknownPlayerIds, 1, newIds, 0, newIds.Length);
            UnknownPlayerIds = newIds;
        }

        void RemoveUnknownId(int playerId)
        {
#if DEBUG_PLAYER_COUNTER
            Log($"RemoveUnknownId playerId={playerId}");
#endif
            UnknownPlayerIds = RemoveFromArray(UnknownPlayerIds, (short)playerId);
        }

        short[] AddToArray(short[] array, short value)
        {
            var index = Array.IndexOf(array, value);
            if (index != -1) return array;
            var newArray = new short[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[array.Length] = value;
            return newArray;
        }

        short[] RemoveFromArray(short[] array, short value)
        {
            var targetIndex = Array.IndexOf(array, value);
            if (targetIndex == -1) return array;
            var newArray = new short[array.Length - 1];
            var newIndex = 0;
            var len = array.Length;
            for (var i = 0; i < len; i++)
            {
                if (i != targetIndex)
                {
                    newArray[newIndex] = array[i];
                    ++newIndex;
                }
            }
            return newArray;
        }

        bool IsOwner()
        {
            return Networking.IsOwner(gameObject);
        }

        void OnCountChanged()
        {
            OnCountChangedCallback.SendCustomEvent("OnCountChanged");
        }

#if DEBUG_PLAYER_COUNTER
        void Log(string log)
        {
            var text = $"PlayerCount MYID={Networking.LocalPlayer.playerId} {System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss\.fff")} {log}";
            Debug.Log(text);
            if (DebugLog != null) DebugLog.text = $"{text}\n{DebugLog.text}";
        }

        string ArrayToString(short[] array)
        {
            var str = "";
            for (var i = 0; i < array.Length; i++)
            {
                str += array[i];
                if (i != array.Length - 1) str += ", ";
            }
            return str;
        }
#endif
    }
}
