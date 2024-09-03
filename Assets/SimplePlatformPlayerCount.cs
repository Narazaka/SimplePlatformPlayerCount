using UdonSharp;
using VRC.SDKBase;
using UnityEngine;
using Narazaka.SimplePlatformPlayerCount.PlayerCount;

namespace Narazaka.SimplePlatformPlayerCount
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SimplePlatformPlayerCount : UdonSharpBehaviour
    {
        [SerializeField]
        PlayerCounter PlayerCounter;
        int PCId;
        int MobileId;

        void Start()
        {
            PCId = VRCShader.PropertyToID("_Udon_SimpleUserCountShader_Count_PC");
            MobileId = VRCShader.PropertyToID("_Udon_SimpleUserCountShader_Count_Mobile");
        }

        public void OnCountChanged()
        {
            VRCShader.SetGlobalFloat(PCId, PlayerCounter.PCPlayerCount);
            VRCShader.SetGlobalFloat(MobileId, PlayerCounter.MobilePlayerCount);
        }
    }
}
