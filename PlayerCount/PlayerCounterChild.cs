using UdonSharp;
using VRC.SDKBase;
using System;
using JetBrains.Annotations;

namespace Narazaka.SimplePlatformPlayerCount.PlayerCount
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerCounterChild : UdonSharpBehaviour
    {
        public PlayerCounter PlayerCounter;
#if DEBUG_PLAYER_COUNTER
        public UnityEngine.UI.Text DebugLog;
#endif
        [UdonSynced, NonSerialized]
        public short PlayerId = -1;
        [UdonSynced, NonSerialized]
        public bool IsMobile;

        [PublicAPI]
        public void NotifyInfo()
        {
#if DEBUG_PLAYER_COUNTER
            Log($"NotifyInfo at [{Networking.LocalPlayer.playerId}]");
#endif
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            NotifyInfoCore();
        }

        void NotifyInfoCore()
        {
            var player = Networking.LocalPlayer;
            PlayerId = (short)player.playerId;
#if DEBUG_PLAYER_COUNTER_FAKE_PLATFORM
            IsMobile = player.playerId % 2 == 0;
#else
#if UNITY_ANDROID || UNITY_IOS
            IsMobile = true;
#else
            IsMobile = false;
#endif
#endif
#if DEBUG_PLAYER_COUNTER
            Log($"NotifyInfoCore PlayerId={PlayerId} IsMobile={IsMobile}");
#endif
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
#if DEBUG_PLAYER_COUNTER
            Log("OnDeserialization");
#endif
            PlayerCounter.OnNotified();
        }

#if DEBUG_PLAYER_COUNTER
        void Log(string log)
        {
            var text = $"PlayerCountChild {System.DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss\.fff")} {log}";
            UnityEngine.Debug.Log(text);
            if (DebugLog != null) DebugLog.text = $"{text}\n{DebugLog.text}";
        }
#endif
    }
}
